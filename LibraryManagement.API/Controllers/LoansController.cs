using Microsoft.AspNetCore.Mvc;
using LibraryManagement.API.Services;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LoansController : ControllerBase
{
    private readonly ILoanRepository _repository;
    private readonly ICacheService _cache;

    public LoansController(ILoanRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] LoanQueryRequest query, CancellationToken ct)
    {
        var paged = await _repository.GetPagedAsync(query, ct);
        return Ok(ApiResponse<PagedResponse<Loan>>.Ok(paged, "Loans retrieved."));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid loan ID." });

        var detail = await _repository.GetDetailByIdAsync(id, ct);
        if (detail is null) return NotFound(new ApiErrorResponse { Message = $"Loan #{id} was not found." });

        return Ok(ApiResponse<LoanDetailResponse>.Ok(detail, "Loan details retrieved."));
    }

    // Never trusts cached copy availability; verifies and updates inside a SQL transaction.
    [HttpPost("issue")]
    public async Task<IActionResult> Issue([FromBody] IssueBookRequest request, CancellationToken ct)
    {
        var loan = await _repository.IssueBookAsync(request, ct);
        _cache.InvalidateGroups(CacheGroups.Books, CacheGroups.Members, CacheGroups.Dashboard, CacheGroups.Reports);
        return CreatedAtAction(nameof(GetById), new { id = loan.LoanId },
            ApiResponse<Loan>.Ok(loan, "Book issued successfully."));
    }

    [HttpPost("return")]
    public async Task<IActionResult> Return([FromBody] ReturnBookRequest request, CancellationToken ct)
    {
        var loan = await _repository.ReturnBookAsync(request, ct);
        _cache.InvalidateGroups(CacheGroups.Books, CacheGroups.Members, CacheGroups.Dashboard, CacheGroups.Reports);
        return Ok(ApiResponse<Loan>.Ok(loan, "Book returned successfully."));
    }

    [HttpPost("{id:int}/renew")]
    public async Task<IActionResult> Renew(int id, [FromBody] RenewLoanRequest? request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new ApiErrorResponse { Message = "Invalid loan ID." });

        var detail = await _repository.RenewLoanAsync(id, request ?? new RenewLoanRequest(), ct);
        _cache.InvalidateGroups(CacheGroups.Books, CacheGroups.Members, CacheGroups.Dashboard, CacheGroups.Reports);
        return Ok(ApiResponse<LoanDetailResponse>.Ok(detail, "Loan renewed successfully."));
    }
}
