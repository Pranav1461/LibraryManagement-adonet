using Microsoft.AspNetCore.Mvc;
using LibraryManagement.API.Services;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BookCopiesController : ControllerBase
{
    private readonly IBookCopyRepository _repository;
    private readonly ICacheService _cache;

    public BookCopiesController(IBookCopyRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] BookCopyQueryRequest query, CancellationToken ct)
    {
        var paged = await _repository.GetPagedAsync(query, ct);
        return Ok(ApiResponse<PagedResponse<BookCopy>>.Ok(paged, "Physical copies retrieved."));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid copy ID." });

        var detail = await _repository.GetDetailByIdAsync(id, ct);
        if (detail is null) return NotFound(new ApiErrorResponse { Message = $"Physical copy #{id} was not found." });

        return Ok(ApiResponse<BookCopyDetailResponse>.Ok(detail, "Physical copy details retrieved."));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookCopyRequest request, CancellationToken ct)
    {
        var created = await _repository.CreateAsync(request, ct);
        _cache.InvalidateGroups(CacheGroups.Books, CacheGroups.Dashboard, CacheGroups.Reports);
        return CreatedAtAction(nameof(GetById), new { id = created.CopyId },
            ApiResponse<BookCopy>.Ok(created, "Physical copy added successfully."));
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreate([FromBody] BulkCreateBookCopiesRequest request, CancellationToken ct)
    {
        var created = await _repository.BulkCreateAsync(request, ct);
        _cache.InvalidateGroups(CacheGroups.Books, CacheGroups.Dashboard, CacheGroups.Reports);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<List<BookCopy>>.Ok(created, $"{created.Count} physical copy/copies added atomically."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookCopyRequest request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid copy ID." });

        var updated = await _repository.UpdateAsync(id, request, ct);
        _cache.InvalidateGroups(CacheGroups.Books, CacheGroups.Dashboard, CacheGroups.Reports);
        return Ok(ApiResponse<BookCopy>.Ok(updated, "Physical copy updated successfully."));
    }
}
