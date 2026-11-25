using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LibraryManagement.API.Configuration;
using LibraryManagement.API.Services;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportRepository _repository;
    private readonly ICacheService _cache;
    private readonly CacheOptions _cacheOptions;

    public ReportsController(IReportRepository repository, ICacheService cache, IOptions<CacheOptions> cacheOptions)
    {
        _repository = repository;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    [HttpGet("books")]
    public IActionResult GetBooksCatalogue([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("books", "book-catalogue.csv", filter, format, () => _repository.GetBookCatalogueReport(filter, null));

    [HttpGet("available-books")]
    public IActionResult GetAvailableBooks([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("available-books", "available-books.csv", filter, format, () => _repository.GetBookCatalogueReport(filter, true));

    [HttpGet("borrowed-books")]
    public IActionResult GetBorrowedBooks([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("borrowed-books", "borrowed-books.csv", filter, format, () => _repository.GetBookCatalogueReport(filter, false));

    [HttpGet("borrowing")]
    public IActionResult GetBorrowing([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("borrowing", "borrowing-report.csv", filter, format, () => _repository.GetLoansReport(filter, null));

    [HttpGet("overdue")]
    public IActionResult GetOverdue([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("overdue", "overdue-books.csv", filter, format, () => _repository.GetLoansReport(filter, "Overdue"));

    [HttpGet("fines")]
    public IActionResult GetFines([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("fines", "fine-report.csv", filter, format, () => _repository.GetLoansReport(filter, "Fines"));

    [HttpGet("members")]
    public IActionResult GetMemberHistory([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("members", "member-borrowing-history.csv", filter, format, () => _repository.GetMemberBorrowingReport(filter));

    [HttpGet("most-borrowed")]
    public IActionResult GetMostBorrowed([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("most-borrowed", "most-borrowed-books.csv", filter, format, () => _repository.GetMostBorrowedBooksReport(filter));

    [HttpGet("categories")]
    public IActionResult GetCategoryReport([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("categories", "category-books.csv", filter, format, () => _repository.GetCategoryReport(filter));

    [HttpGet("authors")]
    public IActionResult GetAuthorReport([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("authors", "author-books.csv", filter, format, () => _repository.GetAuthorReport(filter));

    [HttpGet("monthly-borrowing")]
    public IActionResult GetMonthlyBorrowing([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("monthly-borrowing", "monthly-borrowing.csv", filter, format, () => _repository.GetMonthlyBorrowingReport(filter));

    [HttpGet("monthly-returns")]
    public IActionResult GetMonthlyReturns([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("monthly-returns", "monthly-returns.csv", filter, format, () => _repository.GetMonthlyReturnsReport(filter));

    [HttpGet("copies")]
    public IActionResult GetCopyStatus([FromQuery] ReportFilterRequest filter, [FromQuery] string? format = null) =>
        ServeReport("copies", "physical-copy-status.csv", filter, format, () => _repository.GetCopyStatusReport(filter));

    private IActionResult ServeReport<TRow, TSummary>(
        string keyPrefix,
        string csvFileName,
        ReportFilterRequest filter,
        string? format,
        Func<ReportResult<TRow, TSummary>> generator)
    {
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            filter.Page = 1;
            filter.PageSize = 500;
        }

        var cacheKey =
            $"reports:{keyPrefix}:{filter.StartDate:yyyyMMdd}:{filter.EndDate:yyyyMMdd}:" +
            $"{filter.BookId}:{filter.CategoryId}:{filter.AuthorId}:{filter.MemberId}:{filter.CopyStatus}:" +
            $"{filter.Search?.Trim().ToLowerInvariant()}:{filter.SortBy}:{filter.SortDirection}:{filter.Page}:{filter.PageSize}";

        var (result, hit) = _cache.GetOrCreate(
            CacheGroups.Reports,
            cacheKey,
            TimeSpan.FromSeconds(_cacheOptions.ReportsExpirationSeconds),
            generator);

        Response.Headers["X-Cache"] = hit ? "HIT" : "MISS";

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = Encoding.UTF8.GetBytes(BuildCsv(result.Rows));
            return File(bytes, "text/csv; charset=utf-8", csvFileName);
        }

        return Ok(ApiResponse<ReportResult<TRow, TSummary>>.Ok(result, $"{result.ReportName} generated.", hit));
    }

    private static string BuildCsv<TRow>(IEnumerable<TRow> rows)
    {
        var props = typeof(TRow).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", props.Select(p => EscapeCsv(p.Name))));

        foreach (var row in rows)
        {
            var values = props.Select(p =>
            {
                var val = p.GetValue(row);
                var str = val switch
                {
                    null => string.Empty,
                    DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                    _ => val.ToString() ?? string.Empty
                };
                return EscapeCsv(str);
            });
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string input) =>
        input.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? $"\"{input.Replace("\"", "\"\"")}\""
            : input;
}
