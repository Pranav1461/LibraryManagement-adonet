using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.Data.Interfaces;

public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync(string? search = null, CancellationToken ct = default);
    Task<Category?> GetByIdAsync(int categoryId, CancellationToken ct = default);
    Task<Category> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<Category> UpdateAsync(int categoryId, UpdateCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(int categoryId, CancellationToken ct = default);
}

public interface IPublisherRepository
{
    Task<List<Publisher>> GetAllAsync(string? search = null, CancellationToken ct = default);
    Task<Publisher?> GetByIdAsync(int publisherId, CancellationToken ct = default);
    Task<List<Book>> GetBooksByPublisherAsync(int publisherId, CancellationToken ct = default);
    Task<Publisher> CreateAsync(CreatePublisherRequest request, CancellationToken ct = default);
    Task<Publisher> UpdateAsync(int publisherId, UpdatePublisherRequest request, CancellationToken ct = default);
    Task DeleteAsync(int publisherId, CancellationToken ct = default);
}

public interface IAuthorRepository
{
    Task<List<Author>> GetAllAsync(string? search = null, CancellationToken ct = default);
    Task<Author?> GetByIdAsync(int authorId, CancellationToken ct = default);
    Task<List<Book>> GetBooksByAuthorAsync(int authorId, CancellationToken ct = default);
    Task<Author> CreateAsync(CreateAuthorRequest request, CancellationToken ct = default);
    Task<Author> UpdateAsync(int authorId, UpdateAuthorRequest request, CancellationToken ct = default);
    Task DeleteAsync(int authorId, CancellationToken ct = default);
}

public interface IBookRepository
{
    Task<PagedResponse<Book>> GetPagedAsync(BookQueryRequest query, CancellationToken ct = default);
    Task<BookDetailResponse?> GetDetailByIdAsync(int bookId, CancellationToken ct = default);
    Task<Book> CreateAsync(CreateBookRequest request, CancellationToken ct = default);
    Task<Book> UpdateAsync(int bookId, UpdateBookRequest request, CancellationToken ct = default);
    Task DeleteOrDeactivateAsync(int bookId, CancellationToken ct = default);
}

public interface IBookCopyRepository
{
    Task<PagedResponse<BookCopy>> GetPagedAsync(BookCopyQueryRequest query, CancellationToken ct = default);
    Task<BookCopyDetailResponse?> GetDetailByIdAsync(int copyId, CancellationToken ct = default);
    Task<BookCopy> CreateAsync(CreateBookCopyRequest request, CancellationToken ct = default);
    Task<List<BookCopy>> BulkCreateAsync(BulkCreateBookCopiesRequest request, CancellationToken ct = default);
    Task<BookCopy> UpdateAsync(int copyId, UpdateBookCopyRequest request, CancellationToken ct = default);
}

public interface IMemberRepository
{
    Task<PagedResponse<Member>> GetPagedAsync(MemberQueryRequest query, CancellationToken ct = default);
    Task<MemberDetailResponse?> GetDetailByIdAsync(int memberId, CancellationToken ct = default);
    Task<Member> CreateAsync(CreateMemberRequest request, CancellationToken ct = default);
    Task<Member> UpdateAsync(int memberId, UpdateMemberRequest request, CancellationToken ct = default);
    Task DeleteOrDeactivateAsync(int memberId, CancellationToken ct = default);
}

public interface ILoanRepository
{
    Task<PagedResponse<Loan>> GetPagedAsync(LoanQueryRequest query, CancellationToken ct = default);
    Task<LoanDetailResponse?> GetDetailByIdAsync(int loanId, CancellationToken ct = default);
    Task<Loan> IssueBookAsync(IssueBookRequest request, CancellationToken ct = default);
    Task<Loan> ReturnBookAsync(ReturnBookRequest request, CancellationToken ct = default);
    Task<LoanDetailResponse> RenewLoanAsync(int loanId, RenewLoanRequest request, CancellationToken ct = default);
}

public interface IReservationRepository
{
    Task<List<Reservation>> GetAllAsync(string? status = null, int? memberId = null, int? bookId = null, CancellationToken ct = default);
    Task<Reservation> CreateAsync(CreateReservationRequest request, CancellationToken ct = default);
    Task<Reservation> CancelAsync(int reservationId, CancellationToken ct = default);
    Task<Reservation> FulfillAsync(int reservationId, CancellationToken ct = default);
}

public interface IReportRepository
{
    ReportResult<BookCatalogueReportRow, BookCatalogueSummary> GetBookCatalogueReport(ReportFilterRequest filter, bool? availableOnly = null);
    ReportResult<LoanReportRow, LoanReportSummary> GetLoansReport(ReportFilterRequest filter, string? mode = null);
    ReportResult<MemberHistoryReportRow, MemberHistorySummary> GetMemberBorrowingReport(ReportFilterRequest filter);
    ReportResult<MostBorrowedBookPoint, int> GetMostBorrowedBooksReport(ReportFilterRequest filter);
    ReportResult<CategoryReportRow, CategoryReportSummary> GetCategoryReport(ReportFilterRequest filter);
    ReportResult<AuthorReportRow, AuthorReportSummary> GetAuthorReport(ReportFilterRequest filter);
    ReportResult<MonthlyActivityRow, MonthlyActivitySummary> GetMonthlyBorrowingReport(ReportFilterRequest filter);
    ReportResult<MonthlyActivityRow, MonthlyActivitySummary> GetMonthlyReturnsReport(ReportFilterRequest filter);
    ReportResult<CopyStatusReportRow, CopyStatusSummary> GetCopyStatusReport(ReportFilterRequest filter);
}

public interface IDashboardRepository
{
    DashboardResponse GetDashboardData();
}
