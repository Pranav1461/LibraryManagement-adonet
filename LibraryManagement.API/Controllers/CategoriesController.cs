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
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryRepository _repository;
    private readonly ICacheService _cache;
    private readonly CacheOptions _cacheOptions;

    public CategoriesController(ICategoryRepository repository, ICacheService cache, IOptions<CacheOptions> cacheOptions)
    {
        _repository = repository;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, CancellationToken ct)
    {
        var key = $"categories:list:{search?.Trim().ToLowerInvariant() ?? "_all"}";
        var (items, hit) = await _cache.GetOrCreateAsync(
            CacheGroups.Categories,
            key,
            TimeSpan.FromSeconds(_cacheOptions.LookupExpirationSeconds),
            () => _repository.GetAllAsync(search, ct));

        Response.Headers["X-Cache"] = hit ? "HIT" : "MISS";
        return Ok(ApiResponse<List<Category>>.Ok(items, "Categories retrieved.", hit));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid category ID." });
        var item = await _repository.GetByIdAsync(id, ct);
        if (item is null) return NotFound(new ApiErrorResponse { Message = $"Category #{id} was not found." });
        return Ok(ApiResponse<Category>.Ok(item, "Category retrieved."));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var created = await _repository.CreateAsync(request, ct);
        _cache.InvalidateGroups(CacheGroups.Categories, CacheGroups.Books, CacheGroups.Dashboard, CacheGroups.Reports);
        return CreatedAtAction(nameof(GetById), new { id = created.CategoryId },
            ApiResponse<Category>.Ok(created, "Category created successfully."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid category ID." });
        var updated = await _repository.UpdateAsync(id, request, ct);
        _cache.InvalidateGroups(CacheGroups.Categories, CacheGroups.Books, CacheGroups.Dashboard, CacheGroups.Reports);
        return Ok(ApiResponse<Category>.Ok(updated, "Category updated successfully."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid category ID." });
        await _repository.DeleteAsync(id, ct);
        _cache.InvalidateGroups(CacheGroups.Categories, CacheGroups.Books, CacheGroups.Dashboard, CacheGroups.Reports);
        return Ok(ApiResponse<object>.Ok(new { categoryId = id }, "Category deleted successfully."));
    }
}
