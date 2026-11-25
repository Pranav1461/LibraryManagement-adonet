using System.Data;
using Microsoft.Data.SqlClient;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.Data.Repositories;

// Disconnected ADO.NET repository for all library reports using SqlDataAdapter + DataSet/DataTable.
public sealed class ReportRepository : IReportRepository
{
    private static readonly Dictionary<string, string> BookSortAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "b.Title",
        ["isbn"] = "b.ISBN",
        ["categoryName"] = "c.CategoryName",
        ["publicationYear"] = "b.PublicationYear"
    };

    private static readonly Dictionary<string, string> LoanSortAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        ["issueDate"] = "l.IssueDate",
        ["dueDate"] = "l.DueDate",
        ["returnDate"] = "l.ReturnDate",
        ["fineAmount"] = "l.FineAmount",
        ["memberName"] = "m.FullName",
        ["bookTitle"] = "b.Title"
    };

    private readonly IDbConnectionFactory _factory;

    public ReportRepository(IDbConnectionFactory factory) => _factory = factory;

    public ReportResult<BookCatalogueReportRow, BookCatalogueSummary> GetBookCatalogueReport(
        ReportFilterRequest filter,
        bool? availableOnly = null)
    {
        var sortCol = BookSortAllowlist.TryGetValue(filter.SortBy ?? string.Empty, out var mapped) ? mapped : "b.Title";
        var sortDir = string.Equals(filter.SortDirection, "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        var sql = $"""
            SELECT
                b.BookId,
                b.Title,
                b.ISBN,
                c.CategoryName,
                ISNULL(p.PublisherName, N'—') AS PublisherName,
                ISNULL((
                    SELECT STRING_AGG(a.AuthorName, N', ') WITHIN GROUP (ORDER BY a.AuthorName)
                    FROM dbo.BookAuthors ba
                    INNER JOIN dbo.Authors a ON a.AuthorId = ba.AuthorId
                    WHERE ba.BookId = b.BookId
                ), N'') AS Authors,
                b.PublicationYear,
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId) AS TotalCopies,
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId AND bc.CopyStatus = N'Available') AS AvailableCopies,
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId AND bc.CopyStatus = N'Borrowed') AS BorrowedCopies,
                b.IsActive
            FROM dbo.Books b
            INNER JOIN dbo.Categories c ON c.CategoryId = b.CategoryId
            LEFT JOIN dbo.Publishers p ON p.PublisherId = b.PublisherId
            WHERE (@CategoryId IS NULL OR b.CategoryId = @CategoryId)
              AND (@BookId IS NULL OR b.BookId = @BookId)
              AND (@AuthorId IS NULL OR EXISTS (SELECT 1 FROM dbo.BookAuthors ba WHERE ba.BookId = b.BookId AND ba.AuthorId = @AuthorId))
              AND (@Search IS NULL OR b.Title LIKE @Pattern OR b.ISBN LIKE @Pattern)
              AND (
                    @AvailableOnly IS NULL
                    OR (@AvailableOnly = 1 AND EXISTS (SELECT 1 FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId AND bc.CopyStatus = N'Available'))
                    OR (@AvailableOnly = 0 AND EXISTS (SELECT 1 FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId AND bc.CopyStatus = N'Borrowed'))
                  )
            ORDER BY {sortCol} {sortDir}, b.BookId ASC;
            """;

        DataSet ds = AdoHelper.FillDataSet(_factory, sql, p =>
        {
            BindCommonFilters(p, filter);
            p.Add("@AvailableOnly", SqlDbType.Bit).Value = AdoHelper.DbNullIfNull(availableOnly);
        }, "BookCatalogue");

        DataTable table = ds.Tables["BookCatalogue"]!;
        var allRows = new List<BookCatalogueReportRow>(table.Rows.Count);
        var summary = new BookCatalogueSummary();

        foreach (DataRow r in table.Rows)
        {
            var row = new BookCatalogueReportRow
            {
                BookId = r.Field<int>("BookId"),
                Title = r.Field<string>("Title") ?? string.Empty,
                ISBN = r.Field<string>("ISBN") ?? string.Empty,
                CategoryName = r.Field<string>("CategoryName") ?? string.Empty,
                PublisherName = r.Field<string>("PublisherName") ?? string.Empty,
                Authors = r.Field<string>("Authors") ?? string.Empty,
                PublicationYear = r.Field<int>("PublicationYear"),
                TotalCopies = r.Field<int>("TotalCopies"),
                AvailableCopies = r.Field<int>("AvailableCopies"),
                BorrowedCopies = r.Field<int>("BorrowedCopies"),
                IsActive = r.Field<bool>("IsActive")
            };

            allRows.Add(row);
            summary.TotalTitles++;
            summary.TotalCopies += row.TotalCopies;
            summary.AvailableCopies += row.AvailableCopies;
            summary.BorrowedCopies += row.BorrowedCopies;
        }

        var reportTitle = availableOnly switch
        {
            true => "Available Books Report",
            false => "Borrowed Books Report",
            _ => "Complete Book Catalogue"
        };

        return BuildPagedResult(reportTitle, filter, allRows, summary);
    }

    public ReportResult<LoanReportRow, LoanReportSummary> GetLoansReport(ReportFilterRequest filter, string? mode = null)
    {
        var sortCol = LoanSortAllowlist.TryGetValue(filter.SortBy ?? string.Empty, out var mapped) ? mapped : "l.IssueDate";
        var sortDir = string.Equals(filter.SortDirection, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        var sql = $"""
            SELECT
                l.LoanId,
                m.MembershipNumber,
                m.FullName AS MemberName,
                bc.AccessionNumber,
                b.Title AS BookTitle,
                c.CategoryName,
                l.IssueDate,
                l.DueDate,
                l.ReturnDate,
                l.LoanStatus,
                CASE
                    WHEN DATEDIFF(DAY, CAST(l.DueDate AS DATE), CAST(COALESCE(l.ReturnDate, SYSUTCDATETIME()) AS DATE)) > 0
                    THEN DATEDIFF(DAY, CAST(l.DueDate AS DATE), CAST(COALESCE(l.ReturnDate, SYSUTCDATETIME()) AS DATE))
                    ELSE 0
                END AS OverdueDays,
                l.FineAmount,
                l.RenewalCount
            FROM dbo.Loans l
            INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            INNER JOIN dbo.Categories c ON c.CategoryId = b.CategoryId
            WHERE (@StartDate IS NULL OR l.IssueDate >= @StartDate)
              AND (@EndDate IS NULL OR l.IssueDate < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
              AND (@BookId IS NULL OR b.BookId = @BookId)
              AND (@CategoryId IS NULL OR b.CategoryId = @CategoryId)
              AND (@MemberId IS NULL OR m.MemberId = @MemberId)
              AND (@Search IS NULL OR m.FullName LIKE @Pattern OR m.MembershipNumber LIKE @Pattern OR b.Title LIKE @Pattern OR bc.AccessionNumber LIKE @Pattern)
              AND (
                    @Mode IS NULL
                    OR (@Mode = 'Overdue' AND l.ReturnDate IS NULL AND l.DueDate < CAST(SYSUTCDATETIME() AS DATE))
                    OR (@Mode = 'Borrowed' AND l.ReturnDate IS NULL)
                    OR (@Mode = 'Fines' AND (l.FineAmount > 0 OR (l.ReturnDate IS NULL AND l.DueDate < CAST(SYSUTCDATETIME() AS DATE))))
                  )
            ORDER BY {sortCol} {sortDir}, l.LoanId DESC;
            """;

        DataTable table = AdoHelper.FillDataTable(_factory, sql, p =>
        {
            BindCommonFilters(p, filter);
            p.Add("@Mode", SqlDbType.NVarChar, 20).Value = AdoHelper.DbNullIfNull(mode);
        }, "LoansReport");

        var allRows = new List<LoanReportRow>(table.Rows.Count);
        var summary = new LoanReportSummary();

        foreach (DataRow r in table.Rows)
        {
            var returnDate = r.IsNull("ReturnDate") ? (DateTime?)null : r.Field<DateTime>("ReturnDate");
            var dueDate = r.Field<DateTime>("DueDate");
            var overdueDays = r.Field<int>("OverdueDays");
            var fine = r.Field<decimal>("FineAmount");

            allRows.Add(new LoanReportRow
            {
                LoanId = r.Field<int>("LoanId"),
                MembershipNumber = r.Field<string>("MembershipNumber") ?? string.Empty,
                MemberName = r.Field<string>("MemberName") ?? string.Empty,
                AccessionNumber = r.Field<string>("AccessionNumber") ?? string.Empty,
                BookTitle = r.Field<string>("BookTitle") ?? string.Empty,
                CategoryName = r.Field<string>("CategoryName") ?? string.Empty,
                IssueDate = r.Field<DateTime>("IssueDate"),
                DueDate = dueDate,
                ReturnDate = returnDate,
                LoanStatus = returnDate is null && dueDate.Date < DateTime.UtcNow.Date
                    ? "Overdue"
                    : r.Field<string>("LoanStatus") ?? string.Empty,
                OverdueDays = overdueDays,
                FineAmount = fine,
                RenewalCount = r.Field<int>("RenewalCount")
            });

            summary.TotalLoans++;
            if (returnDate is null)
            {
                summary.ActiveLoans++;
                if (dueDate.Date < DateTime.UtcNow.Date) summary.OverdueLoans++;
            }
            else
            {
                summary.ReturnedLoans++;
            }
            summary.TotalFines += fine;
        }

        var reportName = mode switch
        {
            "Overdue" => "Overdue Books Report",
            "Borrowed" => "Active Borrowed Books Report",
            "Fines" => "Fine Report",
            _ => "Borrowing Activity Report"
        };

        return BuildPagedResult(reportName, filter, allRows, summary);
    }

    public ReportResult<MemberHistoryReportRow, MemberHistorySummary> GetMemberBorrowingReport(ReportFilterRequest filter)
    {
        const string sql = """
            SELECT
                m.MemberId,
                m.MembershipNumber,
                m.FullName,
                m.MembershipStatus,
                COUNT(l.LoanId) AS TotalBorrowed,
                ISNULL(SUM(CASE WHEN l.LoanId IS NOT NULL AND l.ReturnDate IS NULL THEN 1 ELSE 0 END), 0) AS CurrentlyBorrowed,
                ISNULL(SUM(CASE WHEN l.LoanId IS NOT NULL AND l.ReturnDate IS NULL AND l.DueDate < CAST(SYSUTCDATETIME() AS DATE) THEN 1 ELSE 0 END), 0) AS OverdueCount,
                ISNULL(SUM(l.FineAmount), 0) AS TotalFines,
                MAX(l.IssueDate) AS LastBorrowDate
            FROM dbo.Members m
            LEFT JOIN dbo.Loans l ON l.MemberId = m.MemberId
                AND (@StartDate IS NULL OR l.IssueDate >= @StartDate)
                AND (@EndDate IS NULL OR l.IssueDate < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
            WHERE (@MemberId IS NULL OR m.MemberId = @MemberId)
              AND (@Search IS NULL OR m.FullName LIKE @Pattern OR m.MembershipNumber LIKE @Pattern)
            GROUP BY m.MemberId, m.MembershipNumber, m.FullName, m.MembershipStatus
            ORDER BY TotalBorrowed DESC, m.FullName ASC;
            """;

        DataTable table = AdoHelper.FillDataTable(_factory, sql, p => BindCommonFilters(p, filter), "MemberHistory");
        var allRows = new List<MemberHistoryReportRow>(table.Rows.Count);
        var summary = new MemberHistorySummary();

        foreach (DataRow r in table.Rows)
        {
            var row = new MemberHistoryReportRow
            {
                MemberId = r.Field<int>("MemberId"),
                MembershipNumber = r.Field<string>("MembershipNumber") ?? string.Empty,
                FullName = r.Field<string>("FullName") ?? string.Empty,
                MembershipStatus = r.Field<string>("MembershipStatus") ?? string.Empty,
                TotalBorrowed = r.Field<int>("TotalBorrowed"),
                CurrentlyBorrowed = r.Field<int>("CurrentlyBorrowed"),
                OverdueCount = r.Field<int>("OverdueCount"),
                TotalFines = r.Field<decimal>("TotalFines"),
                LastBorrowDate = r.IsNull("LastBorrowDate") ? null : r.Field<DateTime>("LastBorrowDate")
            };

            allRows.Add(row);
            summary.TotalMembers++;
            summary.TotalLoans += row.TotalBorrowed;
            summary.ActiveLoans += row.CurrentlyBorrowed;
            summary.TotalFines += row.TotalFines;
        }

        return BuildPagedResult("Member Borrowing History Report", filter, allRows, summary);
    }

    public ReportResult<MostBorrowedBookPoint, int> GetMostBorrowedBooksReport(ReportFilterRequest filter)
    {
        const string sql = """
            SELECT
                b.BookId,
                b.Title,
                b.ISBN,
                COUNT(l.LoanId) AS LoanCount
            FROM dbo.Books b
            LEFT JOIN dbo.BookCopies bc ON bc.BookId = b.BookId
            LEFT JOIN dbo.Loans l ON l.CopyId = bc.CopyId
                AND (@StartDate IS NULL OR l.IssueDate >= @StartDate)
                AND (@EndDate IS NULL OR l.IssueDate < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
            WHERE (@CategoryId IS NULL OR b.CategoryId = @CategoryId)
              AND (@Search IS NULL OR b.Title LIKE @Pattern OR b.ISBN LIKE @Pattern)
            GROUP BY b.BookId, b.Title, b.ISBN
            ORDER BY LoanCount DESC, b.Title ASC;
            """;

        DataTable table = AdoHelper.FillDataTable(_factory, sql, p => BindCommonFilters(p, filter), "MostBorrowed");
        var allRows = new List<MostBorrowedBookPoint>(table.Rows.Count);
        var totalLoans = 0;

        foreach (DataRow r in table.Rows)
        {
            var count = r.Field<int>("LoanCount");
            allRows.Add(new MostBorrowedBookPoint
            {
                BookId = r.Field<int>("BookId"),
                Title = r.Field<string>("Title") ?? string.Empty,
                ISBN = r.Field<string>("ISBN") ?? string.Empty,
                LoanCount = count
            });
            totalLoans += count;
        }

        return BuildPagedResult("Most Borrowed Books Report", filter, allRows, totalLoans);
    }

    public ReportResult<CategoryReportRow, CategoryReportSummary> GetCategoryReport(ReportFilterRequest filter)
    {
        const string sql = """
            SELECT
                c.CategoryId,
                c.CategoryName,
                COUNT(DISTINCT b.BookId) AS BookCount,
                COUNT(DISTINCT bc.CopyId) AS TotalCopies,
                COUNT(DISTINCT CASE WHEN bc.CopyStatus = N'Available' THEN bc.CopyId END) AS AvailableCopies,
                COUNT(DISTINCT CASE WHEN bc.CopyStatus = N'Borrowed' THEN bc.CopyId END) AS BorrowedCopies,
                COUNT(l.LoanId) AS TotalLoans
            FROM dbo.Categories c
            LEFT JOIN dbo.Books b ON b.CategoryId = c.CategoryId AND b.IsActive = 1
            LEFT JOIN dbo.BookCopies bc ON bc.BookId = b.BookId
            LEFT JOIN dbo.Loans l ON l.CopyId = bc.CopyId
            WHERE (@CategoryId IS NULL OR c.CategoryId = @CategoryId)
              AND (@Search IS NULL OR c.CategoryName LIKE @Pattern)
            GROUP BY c.CategoryId, c.CategoryName
            ORDER BY BookCount DESC, c.CategoryName ASC;
            """;

        DataTable table = AdoHelper.FillDataTable(_factory, sql, p => BindCommonFilters(p, filter), "CategoryReport");
        var allRows = new List<CategoryReportRow>(table.Rows.Count);
        var summary = new CategoryReportSummary();

        foreach (DataRow r in table.Rows)
        {
            var row = new CategoryReportRow
            {
                CategoryId = r.Field<int>("CategoryId"),
                CategoryName = r.Field<string>("CategoryName") ?? string.Empty,
                BookCount = r.Field<int>("BookCount"),
                TotalCopies = r.Field<int>("TotalCopies"),
                AvailableCopies = r.Field<int>("AvailableCopies"),
                BorrowedCopies = r.Field<int>("BorrowedCopies"),
                TotalLoans = r.Field<int>("TotalLoans")
            };

            allRows.Add(row);
            summary.TotalCategories++;
            summary.TotalBooks += row.BookCount;
            summary.TotalCopies += row.TotalCopies;
            summary.TotalLoans += row.TotalLoans;
        }

        return BuildPagedResult("Category-Wise Book Report", filter, allRows, summary);
    }

    public ReportResult<AuthorReportRow, AuthorReportSummary> GetAuthorReport(ReportFilterRequest filter)
    {
        const string sql = """
            SELECT
                a.AuthorId,
                a.AuthorName,
                COUNT(DISTINCT ba.BookId) AS BookCount,
                COUNT(DISTINCT bc.CopyId) AS TotalCopies,
                COUNT(l.LoanId) AS TotalBorrows
            FROM dbo.Authors a
            LEFT JOIN dbo.BookAuthors ba ON ba.AuthorId = a.AuthorId
            LEFT JOIN dbo.BookCopies bc ON bc.BookId = ba.BookId
            LEFT JOIN dbo.Loans l ON l.CopyId = bc.CopyId
            WHERE (@AuthorId IS NULL OR a.AuthorId = @AuthorId)
              AND (@Search IS NULL OR a.AuthorName LIKE @Pattern)
            GROUP BY a.AuthorId, a.AuthorName
            ORDER BY BookCount DESC, a.AuthorName ASC;
            """;

        DataTable table = AdoHelper.FillDataTable(_factory, sql, p => BindCommonFilters(p, filter), "AuthorReport");
        var allRows = new List<AuthorReportRow>(table.Rows.Count);
        var summary = new AuthorReportSummary();

        foreach (DataRow r in table.Rows)
        {
            var row = new AuthorReportRow
            {
                AuthorId = r.Field<int>("AuthorId"),
                AuthorName = r.Field<string>("AuthorName") ?? string.Empty,
                BookCount = r.Field<int>("BookCount"),
                TotalCopies = r.Field<int>("TotalCopies"),
                TotalBorrows = r.Field<int>("TotalBorrows")
            };

            allRows.Add(row);
            summary.TotalAuthors++;
            summary.TotalBookLinks += row.BookCount;
            summary.TotalBorrows += row.TotalBorrows;
        }

        return BuildPagedResult("Author-Wise Book Report", filter, allRows, summary);
    }

    public ReportResult<MonthlyActivityRow, MonthlyActivitySummary> GetMonthlyBorrowingReport(ReportFilterRequest filter)
    {
        const string sql = """
            SELECT
                FORMAT(l.IssueDate, 'yyyy-MM') AS MonthLabel,
                COUNT(1) AS TransactionCount,
                COUNT(DISTINCT l.MemberId) AS UniqueMembers,
                COUNT(DISTINCT bc.BookId) AS UniqueBooks,
                ISNULL(SUM(l.FineAmount), 0) AS FinesCollected
            FROM dbo.Loans l
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            WHERE (@StartDate IS NULL OR l.IssueDate >= @StartDate)
              AND (@EndDate IS NULL OR l.IssueDate < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
            GROUP BY FORMAT(l.IssueDate, 'yyyy-MM')
            ORDER BY MonthLabel DESC;
            """;

        return BuildMonthlyResult("Monthly Borrowing Report", sql, filter);
    }

    public ReportResult<MonthlyActivityRow, MonthlyActivitySummary> GetMonthlyReturnsReport(ReportFilterRequest filter)
    {
        const string sql = """
            SELECT
                FORMAT(l.ReturnDate, 'yyyy-MM') AS MonthLabel,
                COUNT(1) AS TransactionCount,
                COUNT(DISTINCT l.MemberId) AS UniqueMembers,
                COUNT(DISTINCT bc.BookId) AS UniqueBooks,
                ISNULL(SUM(l.FineAmount), 0) AS FinesCollected
            FROM dbo.Loans l
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            WHERE l.ReturnDate IS NOT NULL
              AND (@StartDate IS NULL OR l.ReturnDate >= @StartDate)
              AND (@EndDate IS NULL OR l.ReturnDate < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
            GROUP BY FORMAT(l.ReturnDate, 'yyyy-MM')
            ORDER BY MonthLabel DESC;
            """;

        return BuildMonthlyResult("Monthly Return Report", sql, filter);
    }

    public ReportResult<CopyStatusReportRow, CopyStatusSummary> GetCopyStatusReport(ReportFilterRequest filter)
    {
        const string sql = """
            SELECT
                bc.CopyId,
                bc.AccessionNumber,
                b.Title AS BookTitle,
                b.ISBN,
                c.CategoryName,
                bc.ShelfLocation,
                bc.CopyStatus,
                bc.AcquisitionDate,
                (SELECT COUNT(1) FROM dbo.Loans l WHERE l.CopyId = bc.CopyId) AS TotalLoans
            FROM dbo.BookCopies bc
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            INNER JOIN dbo.Categories c ON c.CategoryId = b.CategoryId
            WHERE (@BookId IS NULL OR bc.BookId = @BookId)
              AND (@CategoryId IS NULL OR b.CategoryId = @CategoryId)
              AND (@CopyStatus IS NULL OR bc.CopyStatus = @CopyStatus)
              AND (@Search IS NULL OR bc.AccessionNumber LIKE @Pattern OR b.Title LIKE @Pattern OR b.ISBN LIKE @Pattern)
            ORDER BY bc.AccessionNumber ASC;
            """;

        DataTable table = AdoHelper.FillDataTable(_factory, sql, p =>
        {
            BindCommonFilters(p, filter);
            var status = string.IsNullOrWhiteSpace(filter.CopyStatus) ? null : filter.CopyStatus.Trim();
            p.Add("@CopyStatus", SqlDbType.NVarChar, 20).Value = AdoHelper.DbNullIfNull(status);
        }, "CopyStatus");

        var allRows = new List<CopyStatusReportRow>(table.Rows.Count);
        var summary = new CopyStatusSummary();

        foreach (DataRow r in table.Rows)
        {
            var status = r.Field<string>("CopyStatus") ?? string.Empty;
            var row = new CopyStatusReportRow
            {
                CopyId = r.Field<int>("CopyId"),
                AccessionNumber = r.Field<string>("AccessionNumber") ?? string.Empty,
                BookTitle = r.Field<string>("BookTitle") ?? string.Empty,
                ISBN = r.Field<string>("ISBN") ?? string.Empty,
                CategoryName = r.Field<string>("CategoryName") ?? string.Empty,
                ShelfLocation = r.IsNull("ShelfLocation") ? null : r.Field<string>("ShelfLocation"),
                CopyStatus = status,
                AcquisitionDate = r.Field<DateTime>("AcquisitionDate"),
                TotalLoans = r.Field<int>("TotalLoans")
            };

            allRows.Add(row);
            summary.TotalCopies++;
            if (status == "Available") summary.Available++;
            else if (status == "Borrowed") summary.Borrowed++;
            else summary.UnavailableOrOther++;
        }

        return BuildPagedResult("Physical Copy Status Report", filter, allRows, summary);
    }

    private ReportResult<MonthlyActivityRow, MonthlyActivitySummary> BuildMonthlyResult(
        string reportName,
        string sql,
        ReportFilterRequest filter)
    {
        DataTable table = AdoHelper.FillDataTable(_factory, sql, p => BindCommonFilters(p, filter), "MonthlyActivity");
        var allRows = new List<MonthlyActivityRow>(table.Rows.Count);
        var summary = new MonthlyActivitySummary();

        foreach (DataRow r in table.Rows)
        {
            var row = new MonthlyActivityRow
            {
                MonthLabel = r.Field<string>("MonthLabel") ?? string.Empty,
                TransactionCount = r.Field<int>("TransactionCount"),
                UniqueMembers = r.Field<int>("UniqueMembers"),
                UniqueBooks = r.Field<int>("UniqueBooks"),
                FinesCollected = r.Field<decimal>("FinesCollected")
            };

            allRows.Add(row);
            summary.TotalMonths++;
            summary.TotalTransactions += row.TransactionCount;
            summary.TotalFines += row.FinesCollected;
        }

        return BuildPagedResult(reportName, filter, allRows, summary);
    }

    private static ReportResult<TRow, TSummary> BuildPagedResult<TRow, TSummary>(
        string reportName,
        ReportFilterRequest filter,
        List<TRow> allRows,
        TSummary summary)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 500 ? 50 : filter.PageSize;
        var pagedRows = allRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new ReportResult<TRow, TSummary>
        {
            ReportName = reportName,
            Page = page,
            PageSize = pageSize,
            TotalCount = allRows.Count,
            Summary = summary,
            Rows = pagedRows
        };
    }

    private static void BindCommonFilters(SqlParameterCollection p, ReportFilterRequest filter)
    {
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        p.Add("@StartDate", SqlDbType.DateTime2).Value = AdoHelper.DbNullIfNull(filter.StartDate?.Date);
        p.Add("@EndDate", SqlDbType.DateTime2).Value = AdoHelper.DbNullIfNull(filter.EndDate?.Date);
        p.Add("@BookId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(filter.BookId);
        p.Add("@CategoryId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(filter.CategoryId);
        p.Add("@AuthorId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(filter.AuthorId);
        p.Add("@MemberId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(filter.MemberId);
        p.Add("@Search", SqlDbType.NVarChar, 300).Value = AdoHelper.DbNullIfNull(search);
        p.Add("@Pattern", SqlDbType.NVarChar, 302).Value = search is null ? DBNull.Value : $"%{search}%";
    }
}
