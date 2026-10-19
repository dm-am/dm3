using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Statistics;

/// <summary>
/// Community statistics and leaderboards
/// </summary>
/// <remarks>
/// Provides endpoints for real-time statistics and historical leaderboards.
/// </remarks>
[ApiController]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Statistics")]
public class StatisticsController : ControllerBase
{
    private readonly CommunityStatsApiService _statsService;

    public StatisticsController(CommunityStatsApiService statsService)
    {
        _statsService = statsService;
    }

    /// <summary>
    /// Get community statistics
    /// </summary>
    /// <remarks>
    /// Returns real-time statistics including online users, totals with today's delta,
    /// and weekly best post.
    /// </remarks>
    /// <response code="200">Statistics retrieved successfully</response>
    [HttpGet("v1/stats", Name = nameof(GetStats))]
    [ProducesResponseType(typeof(Envelope<LiveStats>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats() =>
        Ok(await _statsService.GetLiveStats());

    /// <summary>
    /// Get yearly leaderboards
    /// </summary>
    /// <remarks>
    /// Pass <c>year = 0</c> for the all-time leaderboards aggregated across the
    /// full data set (no date bounds).
    /// </remarks>
    /// <param name="year">Year (e.g., 2025), or 0 for all-time</param>
    /// <response code="200">Yearly leaderboards</response>
    /// <response code="400">Year out of range</response>
    [HttpGet("v1/leaderboards/{year:int}", Name = nameof(GetYearlyLeaderboards))]
    [ProducesResponseType(typeof(Envelope<Leaderboards>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetYearlyLeaderboards(int year) =>
        Ok(await _statsService.GetLeaderboards(year, null));

    /// <summary>
    /// Get monthly leaderboards
    /// </summary>
    /// <param name="year">Year (e.g., 2025)</param>
    /// <param name="month">Month (1-12)</param>
    /// <response code="200">Monthly leaderboards</response>
    /// <response code="400">Year or month out of range</response>
    [HttpGet("v1/leaderboards/{year:int}/{month:int}", Name = nameof(GetMonthlyLeaderboards))]
    [ProducesResponseType(typeof(Envelope<Leaderboards>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonthlyLeaderboards(int year, int month) =>
        Ok(await _statsService.GetLeaderboards(year, month));
}
