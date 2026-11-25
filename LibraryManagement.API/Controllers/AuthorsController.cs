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
public sealed class AuthorsController : ControllerBase
{
    private readonly IAuthorRepository _repository;
    private readonly ICacheService _cache;
    private readonly CacheOptions _cacheOptions;

    public AuthorsController(IAuthorRepository repository, ICacheService cache, IOptions<CacheOptions> cacheOptions)
    {
        _repository = repository;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, CancellationToken ct)
    {
        var key = $"authors:list:{search?.Trim().ToLowerInvariant() ?? "_all"}";
        var (items, hit) = await _cache.GetOrCreateAsync(
            CacheGroups.Authors,
            key,
            TimeSpan.FromSeconds(_cacheOptions.LookupExpirationSeconds),
            () => _repository.GetAllAsync(search, ct));

        Response.Headers["X-Cache"] = hit ? "HIT" : "MISS";
        return Ok(ApiResponse<List<Author>>.Ok(items, "Authors retrieved.", hit));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid author ID." });
        var item = await _repository.GetByIdAsync(id, ct);
        if (item is null) return NotFound(new ApiErrorResponse { Message = $"Author #{id} was not found." });
        return Ok(ApiResponse<Author>.Ok(item, "Author retrieved."));
    }

    [HttpGet("{id:int}/books")]
    public async Task<IActionResult> GetBooks(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid author ID." });
        var author = await _repository.GetByIdAsync(id, ct);
        if (author is null) return NotFound(new ApiErrorResponse { Message = $"Author #{id} was not found." });

        var books = await _repository.GetBooksByAuthorAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { author, books }, "Author books retrieved."));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAuthorRequest request, CancellationToken ct)
    {
        var created = await _repository.CreateAsync(request, ct);
        _cache.InvalidateGroups(CacheGroups.Authors, CacheGroups.Books, CacheGroups.Reports);
        return CreatedAtAction(nameof(GetById), new { id = created.AuthorId },
            ApiResponse<Author>.Ok(created, "Author created successfully."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAuthorRequest request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid author ID." });
        var updated = await _repository.UpdateAsync(id, request, ct);
        _cache.InvalidateGroups(CacheGroups.Authors, CacheGroups.Books, CacheGroups.Reports);
        return Ok(ApiResponse<Author>.Ok(updated, "Author updated successfully."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid author ID." });
        await _repository.DeleteAsync(id, ct);
        _cache.InvalidateGroups(CacheGroups.Authors, CacheGroups.Books, CacheGroups.Reports);
        return Ok(ApiResponse<object>.Ok(new { authorId = id }, "Author deleted successfully."));
    }
}
