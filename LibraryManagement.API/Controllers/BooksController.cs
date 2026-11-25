using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LibraryManagement.API.Configuration;
using LibraryManagement.API.Services;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BooksController : ControllerBase
{
    private readonly IBookRepository _repository;
    private readonly ICacheService _cache;
    private readonly CacheOptions _cacheOptions;

    public BooksController(IBookRepository repository, ICacheService cache, IOptions<CacheOptions> cacheOptions)
    {
        _repository = repository;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] BookQueryRequest query, CancellationToken ct)
    {
        var key = $"books:paged:{query.Search?.Trim().ToLowerInvariant() ?? "_"}:{query.CategoryId}:" +
                  $"{query.PublisherId}:{query.AuthorId}:{query.IncludeInactive}:{query.SortBy}:{query.SortDirection}:{query.Page}:{query.PageSize}";

        var (paged, hit) = await _cache.GetOrCreateAsync(
            CacheGroups.Books,
            key,
            TimeSpan.FromSeconds(_cacheOptions.BooksExpirationSeconds),
            () => _repository.GetPagedAsync(query, ct));

        Response.Headers["X-Cache"] = hit ? "HIT" : "MISS";
        return Ok(ApiResponse<PagedResponse<Book>>.Ok(paged, "Books retrieved.", hit));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid book ID." });

        var detail = await _repository.GetDetailByIdAsync(id, ct);
        if (detail is null) return NotFound(new ApiErrorResponse { Message = $"Book #{id} was not found." });

        return Ok(ApiResponse<BookDetailResponse>.Ok(detail, "Book details retrieved."));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookRequest request, CancellationToken ct)
    {
        var created = await _repository.CreateAsync(request, ct);
        InvalidateCaches();
        return CreatedAtAction(nameof(GetById), new { id = created.BookId },
            ApiResponse<Book>.Ok(created, "Book created successfully."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookRequest request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid book ID." });

        var updated = await _repository.UpdateAsync(id, request, ct);
        InvalidateCaches();
        return Ok(ApiResponse<Book>.Ok(updated, "Book updated successfully."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid book ID." });

        await _repository.DeleteOrDeactivateAsync(id, ct);
        InvalidateCaches();
        return Ok(ApiResponse<object>.Ok(new { bookId = id }, "Book removed or deactivated successfully."));
    }

    private void InvalidateCaches() =>
        _cache.InvalidateGroups(
            CacheGroups.Books,
            CacheGroups.Categories,
            CacheGroups.Authors,
            CacheGroups.Publishers,
            CacheGroups.Dashboard,
            CacheGroups.Reports);
}
