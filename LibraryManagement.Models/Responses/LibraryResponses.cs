using LibraryManagement.Models.Entities;

namespace LibraryManagement.Models.Responses;

public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public bool? CacheHit { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success", bool? cacheHit = null) => new()
    {
        Success = true,
        Message = message,
        Data = data,
        CacheHit = cacheHit
    };
}

public class ApiErrorResponse
{
    public bool Success { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public IDictionary<string, string[]>? Errors { get; set; }
    public string? TraceId { get; set; }
}

public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class BookDetailResponse
{
    public Book Book { get; set; } = new();
    public List<BookCopy> Copies { get; set; } = new();
}

public class BookCopyDetailResponse
{
    public BookCopy Copy { get; set; } = new();
    public List<Loan> LoanHistory { get; set; } = new();
}

public class MemberDetailResponse
{
    public Member Member { get; set; } = new();
    public List<Loan> CurrentLoans { get; set; } = new();
    public List<Loan> OverdueLoans { get; set; } = new();
    public List<Loan> BorrowingHistory { get; set; } = new();
}

public class LoanDetailResponse
{
    public Loan Loan { get; set; } = new();
    public List<LoanRenewal> Renewals { get; set; } = new();
}

public class LibraryPolicyResponse
{
    public int DefaultBorrowingDays { get; set; }
    public int MaxActiveLoansPerMember { get; set; }
    public decimal DailyFineRate { get; set; }
    public int MaxRenewals { get; set; }
    public int DefaultRenewalDays { get; set; }
}

public class DashboardResponse
{
    public DashboardSummary Summary { get; set; } = new();
    public List<MonthlyCountPoint> MonthlyBorrowing { get; set; } = new();
    public List<MonthlyCountPoint> MonthlyReturns { get; set; } = new();
    public List<MostBorrowedBookPoint> MostBorrowedBooks { get; set; } = new();
    public List<CategoryBookPoint> BooksByCategory { get; set; } = new();
    public List<Loan> RecentBorrowings { get; set; } = new();
    public List<Loan> RecentReturns { get; set; } = new();
    public List<Loan> OverdueAlerts { get; set; } = new();
    public List<Loan> DueSoonLoans { get; set; } = new();
}

public class DashboardSummary
{
    public int TotalBooks { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int BorrowedCopies { get; set; }
    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public int OverdueLoans { get; set; }
    public int BooksDueToday { get; set; }
    public int TotalBorrowings { get; set; }
    public int TotalReturns { get; set; }
}

public class MonthlyCountPoint
{
    public string MonthLabel { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class MostBorrowedBookPoint
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public int LoanCount { get; set; }
}

public class CategoryBookPoint
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int BookCount { get; set; }
    public int CopyCount { get; set; }
}

public class ReportResult<TRow, TSummary>
{
    public string ReportName { get; set; } = string.Empty;
    public string DataAccessMode { get; set; } = "Disconnected ADO.NET (SqlDataAdapter + DataSet/DataTable)";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public TSummary Summary { get; set; } = default!;
    public List<TRow> Rows { get; set; } = new();
}

public class BookCatalogueReportRow
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string PublisherName { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public int PublicationYear { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int BorrowedCopies { get; set; }
    public bool IsActive { get; set; }
}

public class BookCatalogueSummary
{
    public int TotalTitles { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int BorrowedCopies { get; set; }
}

public class LoanReportRow
{
    public int LoanId { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string AccessionNumber { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string LoanStatus { get; set; } = string.Empty;
    public int OverdueDays { get; set; }
    public decimal FineAmount { get; set; }
    public int RenewalCount { get; set; }
}

public class LoanReportSummary
{
    public int TotalLoans { get; set; }
    public int ActiveLoans { get; set; }
    public int OverdueLoans { get; set; }
    public int ReturnedLoans { get; set; }
    public decimal TotalFines { get; set; }
}

public class MemberHistoryReportRow
{
    public int MemberId { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MembershipStatus { get; set; } = string.Empty;
    public int TotalBorrowed { get; set; }
    public int CurrentlyBorrowed { get; set; }
    public int OverdueCount { get; set; }
    public decimal TotalFines { get; set; }
    public DateTime? LastBorrowDate { get; set; }
}

public class MemberHistorySummary
{
    public int TotalMembers { get; set; }
    public int TotalLoans { get; set; }
    public int ActiveLoans { get; set; }
    public decimal TotalFines { get; set; }
}

public class AuthorReportRow
{
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int BookCount { get; set; }
    public int TotalCopies { get; set; }
    public int TotalBorrows { get; set; }
}

public class AuthorReportSummary
{
    public int TotalAuthors { get; set; }
    public int TotalBookLinks { get; set; }
    public int TotalBorrows { get; set; }
}

public class CategoryReportRow
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int BookCount { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int BorrowedCopies { get; set; }
    public int TotalLoans { get; set; }
}

public class CategoryReportSummary
{
    public int TotalCategories { get; set; }
    public int TotalBooks { get; set; }
    public int TotalCopies { get; set; }
    public int TotalLoans { get; set; }
}

public class MonthlyActivityRow
{
    public string MonthLabel { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public int UniqueMembers { get; set; }
    public int UniqueBooks { get; set; }
    public decimal FinesCollected { get; set; }
}

public class MonthlyActivitySummary
{
    public int TotalMonths { get; set; }
    public int TotalTransactions { get; set; }
    public decimal TotalFines { get; set; }
}

public class CopyStatusReportRow
{
    public int CopyId { get; set; }
    public string AccessionNumber { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ShelfLocation { get; set; }
    public string CopyStatus { get; set; } = string.Empty;
    public DateTime AcquisitionDate { get; set; }
    public int TotalLoans { get; set; }
}

public class CopyStatusSummary
{
    public int TotalCopies { get; set; }
    public int Available { get; set; }
    public int Borrowed { get; set; }
    public int UnavailableOrOther { get; set; }
}
