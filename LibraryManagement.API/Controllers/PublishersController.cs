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
public sealed class PublishersController : ControllerBase
{
    private readonly IPublisherRepository _repository;
    private readonly ICacheService _cache;
    private readonly CacheOptions _cacheOptions;

    public PublishersController(IPublisherRepository repository, ICacheService cache, IOptions<CacheOptions> cacheOptions)
    {
        _repository = repository;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, CancellationToken ct)
    {
        var key = $"publishers:list:{search?.Trim().ToLowerInvariant() ?? "_all"}";
        var (items, hit) = await _cache.GetOrCreateAsync(
            CacheGroups.Publishers,
            key,
            TimeSpan.FromSeconds(_cacheOptions.LookupExpirationSeconds),
            () => _repository.GetAllAsync(search, ct));

        Response.Headers["X-Cache"] = hit ? "HIT" : "MISS";
        return Ok(ApiResponse<List<Publisher>>.Ok(items, "Publishers retrieved.", hit));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid publisher ID." });
        var item = await _repository.GetByIdAsync(id, ct);
        if (item is null) return NotFound(new ApiErrorResponse { Message = $"Publisher #{id} was not found." });
        return Ok(ApiResponse<Publisher>.Ok(item, "Publisher retrieved."));
    }

    [HttpGet("{id:int}/books")]
    public async Task<IActionResult> GetBooks(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid publisher ID." });
        var publisher = await _repository.GetByIdAsync(id, ct);
        if (publisher is null) return NotFound(new ApiErrorResponse { Message = $"Publisher #{id} was not found." });

        var books = await _repository.GetBooksByPublisherAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { publisher, books }, "Publisher books retrieved."));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePublisherRequest request, CancellationToken ct)
    {
        var created = await _repository.CreateAsync(request, ct);
        _cache.InvalidateGroups(CacheGroups.Publishers, CacheGroups.Books, CacheGroups.Reports);
        return CreatedAtAction(nameof(GetById), new { id = created.PublisherId },
            ApiResponse<Publisher>.Ok(created, "Publisher created successfully."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePublisherRequest request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid publisher ID." });
        var updated = await _repository.UpdateAsync(id, request, ct);
        _cache.InvalidateGroups(CacheGroups.Publishers, CacheGroups.Books, CacheGroups.Reports);
        return Ok(ApiResponse<Publisher>.Ok(updated, "Publisher updated successfully."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid publisher ID." });
        await _repository.DeleteAsync(id, ct);
        _cache.InvalidateGroups(CacheGroups.Publishers, CacheGroups.Books, CacheGroups.Reports);
        return Ok(ApiResponse<object>.Ok(new { publisherId = id }, "Publisher deleted successfully."));
    }
}
