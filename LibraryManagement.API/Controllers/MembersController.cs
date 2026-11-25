using Microsoft.AspNetCore.Mvc;
using LibraryManagement.API.Services;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MembersController : ControllerBase
{
    private readonly IMemberRepository _repository;
    private readonly ICacheService _cache;

    public MembersController(IMemberRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] MemberQueryRequest query, CancellationToken ct)
    {
        var paged = await _repository.GetPagedAsync(query, ct);
        return Ok(ApiResponse<PagedResponse<Member>>.Ok(paged, "Members retrieved."));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid member ID." });

        var detail = await _repository.GetDetailByIdAsync(id, ct);
        if (detail is null) return NotFound(new ApiErrorResponse { Message = $"Member #{id} was not found." });

        return Ok(ApiResponse<MemberDetailResponse>.Ok(detail, "Member details retrieved."));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMemberRequest request, CancellationToken ct)
    {
        var created = await _repository.CreateAsync(request, ct);
        _cache.InvalidateGroups(CacheGroups.Members, CacheGroups.Dashboard, CacheGroups.Reports);
        return CreatedAtAction(nameof(GetById), new { id = created.MemberId },
            ApiResponse<Member>.Ok(created, "Member registered successfully."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMemberRequest request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid member ID." });

        var updated = await _repository.UpdateAsync(id, request, ct);
        _cache.InvalidateGroups(CacheGroups.Members, CacheGroups.Dashboard, CacheGroups.Reports);
        return Ok(ApiResponse<Member>.Ok(updated, "Member updated successfully."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid member ID." });

        await _repository.DeleteOrDeactivateAsync(id, ct);
        _cache.InvalidateGroups(CacheGroups.Members, CacheGroups.Dashboard, CacheGroups.Reports);
        return Ok(ApiResponse<object>.Ok(new { memberId = id }, "Member removed or deactivated successfully."));
    }
}
