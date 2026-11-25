using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LibraryManagement.API.Configuration;
using LibraryManagement.API.Services;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Configuration;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICacheService _cache;
    private readonly CacheOptions _cacheOptions;
    private readonly LibraryPolicySettings _policy;

    public DashboardController(
        IDashboardRepository dashboardRepository,
        ICacheService cache,
        IOptions<CacheOptions> cacheOptions,
        IOptions<LibraryPolicySettings> policy)
    {
        _dashboardRepository = dashboardRepository;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
        _policy = policy.Value;
    }

    [HttpGet]
    public IActionResult GetDashboard()
    {
        var (data, hit) = _cache.GetOrCreate(
            CacheGroups.Dashboard,
            "dashboard:overview",
            TimeSpan.FromSeconds(_cacheOptions.DashboardExpirationSeconds),
            () => _dashboardRepository.GetDashboardData());

        Response.Headers["X-Cache"] = hit ? "HIT" : "MISS";
        return Ok(ApiResponse<DashboardResponse>.Ok(data, "Dashboard loaded.", hit));
    }

    [HttpGet("policy")]
    public IActionResult GetPolicy()
    {
        var dto = new LibraryPolicyResponse
        {
            DefaultBorrowingDays = _policy.DefaultBorrowingDays,
            MaxActiveLoansPerMember = _policy.MaxActiveLoansPerMember,
            DailyFineRate = _policy.DailyFineRate,
            MaxRenewals = _policy.MaxRenewals,
            DefaultRenewalDays = _policy.DefaultRenewalDays
        };

        return Ok(ApiResponse<LibraryPolicyResponse>.Ok(dto, "Library policy retrieved."));
    }

    [HttpPost("cache/clear")]
    public IActionResult ClearCache()
    {
        _cache.InvalidateAll();
        return Ok(ApiResponse<object>.Ok(new { cleared = true }, "Cache cleared."));
    }
}
