using Microsoft.AspNetCore.Mvc;
using LibraryManagement.API.Services;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReservationsController : ControllerBase
{
    private readonly IReservationRepository _repository;
    private readonly ICacheService _cache;

    public ReservationsController(IReservationRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] int? memberId = null,
        [FromQuery] int? bookId = null,
        CancellationToken ct = default)
    {
        var list = await _repository.GetAllAsync(status, memberId, bookId, ct);
        return Ok(ApiResponse<List<Reservation>>.Ok(list, "Reservations retrieved."));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest request, CancellationToken ct)
    {
        var created = await _repository.CreateAsync(request, ct);
        _cache.InvalidateGroups(CacheGroups.Dashboard);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Reservation>.Ok(created, "Reservation created."));
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid reservation ID." });
        var updated = await _repository.CancelAsync(id, ct);
        _cache.InvalidateGroups(CacheGroups.Dashboard);
        return Ok(ApiResponse<Reservation>.Ok(updated, "Reservation cancelled."));
    }

    [HttpPut("{id:int}/fulfill")]
    public async Task<IActionResult> Fulfill(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid reservation ID." });
        var updated = await _repository.FulfillAsync(id, ct);
        _cache.InvalidateGroups(CacheGroups.Dashboard);
        return Ok(ApiResponse<Reservation>.Ok(updated, "Reservation marked fulfilled."));
    }
}
