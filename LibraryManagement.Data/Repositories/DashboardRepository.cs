using System.Data;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.Data.Repositories;

// Disconnected ADO.NET repository for the dashboard using a multi-table DataSet fill.
public sealed class DashboardRepository : IDashboardRepository
{
    private readonly IDbConnectionFactory _factory;

    public DashboardRepository(IDbConnectionFactory factory) => _factory = factory;

    public DashboardResponse GetDashboardData()
    {
        const string sql = """
            -- 0. Summary KPIs
            SELECT
                (SELECT COUNT(1) FROM dbo.Books WHERE IsActive = 1) AS TotalBooks,
                (SELECT COUNT(1) FROM dbo.BookCopies) AS TotalCopies,
                (SELECT COUNT(1) FROM dbo.BookCopies WHERE CopyStatus = N'Available') AS AvailableCopies,
                (SELECT COUNT(1) FROM dbo.BookCopies WHERE CopyStatus = N'Borrowed') AS BorrowedCopies,
                (SELECT COUNT(1) FROM dbo.Members) AS TotalMembers,
                (SELECT COUNT(1) FROM dbo.Members WHERE MembershipStatus = N'Active') AS ActiveMembers,
                (SELECT COUNT(1) FROM dbo.Loans WHERE ReturnDate IS NULL AND DueDate < CAST(SYSUTCDATETIME() AS DATE)) AS OverdueLoans,
                (SELECT COUNT(1) FROM dbo.Loans WHERE ReturnDate IS NULL AND CAST(DueDate AS DATE) = CAST(SYSUTCDATETIME() AS DATE)) AS BooksDueToday,
                (SELECT COUNT(1) FROM dbo.Loans) AS TotalBorrowings,
                (SELECT COUNT(1) FROM dbo.Loans WHERE ReturnDate IS NOT NULL) AS TotalReturns;

            -- 1. Monthly Borrowing
            SELECT
                FORMAT(IssueDate, 'yyyy-MM') AS MonthLabel,
                COUNT(1) AS TxCount
            FROM dbo.Loans
            GROUP BY FORMAT(IssueDate, 'yyyy-MM')
            ORDER BY MonthLabel ASC;

            -- 2. Monthly Returns
            SELECT
                FORMAT(ReturnDate, 'yyyy-MM') AS MonthLabel,
                COUNT(1) AS TxCount
            FROM dbo.Loans
            WHERE ReturnDate IS NOT NULL
            GROUP BY FORMAT(ReturnDate, 'yyyy-MM')
            ORDER BY MonthLabel ASC;

            -- 3. Most Borrowed Books (top 6)
            SELECT TOP 6
                b.BookId,
                b.Title,
                b.ISBN,
                COUNT(l.LoanId) AS LoanCount
            FROM dbo.Books b
            INNER JOIN dbo.BookCopies bc ON bc.BookId = b.BookId
            INNER JOIN dbo.Loans l ON l.CopyId = bc.CopyId
            GROUP BY b.BookId, b.Title, b.ISBN
            ORDER BY LoanCount DESC, b.Title ASC;

            -- 4. Books by Category
            SELECT
                c.CategoryId,
                c.CategoryName,
                COUNT(DISTINCT b.BookId) AS BookCount,
                COUNT(bc.CopyId) AS CopyCount
            FROM dbo.Categories c
            LEFT JOIN dbo.Books b ON b.CategoryId = c.CategoryId AND b.IsActive = 1
            LEFT JOIN dbo.BookCopies bc ON bc.BookId = b.BookId
            GROUP BY c.CategoryId, c.CategoryName
            ORDER BY CopyCount DESC, c.CategoryName ASC;

            -- 5. Recent Borrowings (top 6)
            SELECT TOP 6
                l.LoanId, l.MemberId, m.MembershipNumber, m.FullName,
                l.CopyId, bc.AccessionNumber, b.BookId, b.Title, b.ISBN,
                l.IssueDate, l.DueDate, l.ReturnDate, l.LoanStatus,
                l.FineAmount, l.RenewalCount, l.Notes, l.CreatedAt
            FROM dbo.Loans l
            INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            ORDER BY l.IssueDate DESC, l.LoanId DESC;

            -- 6. Recent Returns (top 6)
            SELECT TOP 6
                l.LoanId, l.MemberId, m.MembershipNumber, m.FullName,
                l.CopyId, bc.AccessionNumber, b.BookId, b.Title, b.ISBN,
                l.IssueDate, l.DueDate, l.ReturnDate, l.LoanStatus,
                l.FineAmount, l.RenewalCount, l.Notes, l.CreatedAt
            FROM dbo.Loans l
            INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            WHERE l.ReturnDate IS NOT NULL
            ORDER BY l.ReturnDate DESC, l.LoanId DESC;

            -- 7. Overdue Alerts
            SELECT
                l.LoanId, l.MemberId, m.MembershipNumber, m.FullName,
                l.CopyId, bc.AccessionNumber, b.BookId, b.Title, b.ISBN,
                l.IssueDate, l.DueDate, l.ReturnDate, l.LoanStatus,
                l.FineAmount, l.RenewalCount, l.Notes, l.CreatedAt
            FROM dbo.Loans l
            INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            WHERE l.ReturnDate IS NULL
              AND l.DueDate < CAST(SYSUTCDATETIME() AS DATE)
            ORDER BY l.DueDate ASC;

            -- 8. Books Due Soon (today through next 3 days)
            SELECT
                l.LoanId, l.MemberId, m.MembershipNumber, m.FullName,
                l.CopyId, bc.AccessionNumber, b.BookId, b.Title, b.ISBN,
                l.IssueDate, l.DueDate, l.ReturnDate, l.LoanStatus,
                l.FineAmount, l.RenewalCount, l.Notes, l.CreatedAt
            FROM dbo.Loans l
            INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            WHERE l.ReturnDate IS NULL
              AND CAST(l.DueDate AS DATE) BETWEEN CAST(SYSUTCDATETIME() AS DATE) AND DATEADD(DAY, 3, CAST(SYSUTCDATETIME() AS DATE))
            ORDER BY l.DueDate ASC;
            """;

        DataSet ds = AdoHelper.FillDataSet(
            _factory,
            sql,
            null,
            "Summary",
            "MonthlyBorrowing",
            "MonthlyReturns",
            "MostBorrowedBooks",
            "BooksByCategory",
            "RecentBorrowings",
            "RecentReturns",
            "OverdueAlerts",
            "DueSoonLoans");

        var response = new DashboardResponse();

        if (ds.Tables["Summary"]?.Rows.Count > 0)
        {
            var r = ds.Tables["Summary"]!.Rows[0];
            response.Summary = new DashboardSummary
            {
                TotalBooks = r.Field<int>("TotalBooks"),
                TotalCopies = r.Field<int>("TotalCopies"),
                AvailableCopies = r.Field<int>("AvailableCopies"),
                BorrowedCopies = r.Field<int>("BorrowedCopies"),
                TotalMembers = r.Field<int>("TotalMembers"),
                ActiveMembers = r.Field<int>("ActiveMembers"),
                OverdueLoans = r.Field<int>("OverdueLoans"),
                BooksDueToday = r.Field<int>("BooksDueToday"),
                TotalBorrowings = r.Field<int>("TotalBorrowings"),
                TotalReturns = r.Field<int>("TotalReturns")
            };
        }

        response.MonthlyBorrowing = MapMonthlyPoints(ds.Tables["MonthlyBorrowing"]);
        response.MonthlyReturns = MapMonthlyPoints(ds.Tables["MonthlyReturns"]);

        if (ds.Tables["MostBorrowedBooks"] is { } mbTable)
        {
            foreach (DataRow r in mbTable.Rows)
            {
                response.MostBorrowedBooks.Add(new MostBorrowedBookPoint
                {
                    BookId = r.Field<int>("BookId"),
                    Title = r.Field<string>("Title") ?? string.Empty,
                    ISBN = r.Field<string>("ISBN") ?? string.Empty,
                    LoanCount = r.Field<int>("LoanCount")
                });
            }
        }

        if (ds.Tables["BooksByCategory"] is { } catTable)
        {
            foreach (DataRow r in catTable.Rows)
            {
                response.BooksByCategory.Add(new CategoryBookPoint
                {
                    CategoryId = r.Field<int>("CategoryId"),
                    CategoryName = r.Field<string>("CategoryName") ?? string.Empty,
                    BookCount = r.Field<int>("BookCount"),
                    CopyCount = r.Field<int>("CopyCount")
                });
            }
        }

        response.RecentBorrowings = MapLoans(ds.Tables["RecentBorrowings"]);
        response.RecentReturns = MapLoans(ds.Tables["RecentReturns"]);
        response.OverdueAlerts = MapLoans(ds.Tables["OverdueAlerts"]);
        response.DueSoonLoans = MapLoans(ds.Tables["DueSoonLoans"]);

        return response;
    }

    private static List<MonthlyCountPoint> MapMonthlyPoints(DataTable? table)
    {
        var list = new List<MonthlyCountPoint>();
        if (table is null) return list;

        foreach (DataRow r in table.Rows)
        {
            list.Add(new MonthlyCountPoint
            {
                MonthLabel = r.Field<string>("MonthLabel") ?? string.Empty,
                Count = r.Field<int>("TxCount")
            });
        }
        return list;
    }

    private static List<Loan> MapLoans(DataTable? table)
    {
        var list = new List<Loan>();
        if (table is null) return list;

        foreach (DataRow r in table.Rows)
        {
            list.Add(new Loan
            {
                LoanId = r.Field<int>("LoanId"),
                MemberId = r.Field<int>("MemberId"),
                MembershipNumber = r.Field<string>("MembershipNumber") ?? string.Empty,
                MemberName = r.Field<string>("FullName") ?? string.Empty,
                CopyId = r.Field<int>("CopyId"),
                AccessionNumber = r.Field<string>("AccessionNumber") ?? string.Empty,
                BookId = r.Field<int>("BookId"),
                BookTitle = r.Field<string>("Title") ?? string.Empty,
                ISBN = r.Field<string>("ISBN") ?? string.Empty,
                IssueDate = r.Field<DateTime>("IssueDate"),
                DueDate = r.Field<DateTime>("DueDate"),
                ReturnDate = r.IsNull("ReturnDate") ? null : r.Field<DateTime>("ReturnDate"),
                LoanStatus = r.Field<string>("LoanStatus") ?? string.Empty,
                FineAmount = r.Field<decimal>("FineAmount"),
                RenewalCount = r.Field<int>("RenewalCount"),
                Notes = r.IsNull("Notes") ? null : r.Field<string>("Notes"),
                CreatedAt = r.Field<DateTime>("CreatedAt")
            });
        }
        return list;
    }
}
