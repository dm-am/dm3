using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
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
    // Wide sanity bounds for the public period routes: the site was founded
    // in 2007, but the API stays permissive (an out-of-range-but-plausible
    // year just returns empty boards). The bounds only reject values that
    // would overflow date arithmetic or are plainly nonsensical.
    private const int MinYear = 1900;
    private const int MaxYear = 2100;

    private readonly ICommunityStatsApiService _statsService;

    /// <inheritdoc />
    public StatisticsController(ICommunityStatsApiService statsService)
    {
        _statsService = statsService;
    }

    #region Live Stats

    /// <summary>
    /// Get community statistics
    /// </summary>
    /// <remarks>
    /// Returns real-time statistics including online users, totals with today's delta,
    /// last reviewed post, and weekly best post.
    /// </remarks>
    /// <response code="200">Statistics retrieved successfully</response>
    [HttpGet("v1/stats", Name = nameof(GetStats))]
    [ProducesResponseType(typeof(Envelope<LiveStats>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats() =>
        Ok(await _statsService.GetLiveStats());

    #endregion

    #region Leaderboards

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
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetYearlyLeaderboards(int year)
    {
        ValidatePeriod(year, null);
        return Ok(await _statsService.GetLeaderboards(year, null));
    }

    /// <summary>
    /// Get monthly leaderboards
    /// </summary>
    /// <param name="year">Year (e.g., 2025)</param>
    /// <param name="month">Month (1-12)</param>
    /// <response code="200">Monthly leaderboards</response>
    /// <response code="400">Year or month out of range</response>
    [HttpGet("v1/leaderboards/{year:int}/{month:int}", Name = nameof(GetMonthlyLeaderboards))]
    [ProducesResponseType(typeof(Envelope<Leaderboards>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonthlyLeaderboards(int year, int month)
    {
        ValidatePeriod(year, month);
        return Ok(await _statsService.GetLeaderboards(year, month));
    }

    /// <summary>
    /// Rejects period values that would overflow date arithmetic (year 9999)
    /// or are plainly invalid (month 13) with a 400 instead of letting
    /// DateTimeOffset construction throw into a 500. year == 0 is the
    /// documented all-time period; a month with year 0 is meaningless.
    /// </summary>
    private static void ValidatePeriod(int year, int? month)
    {
        var errors = new Dictionary<string, string>();

        if (year != 0 && (year < MinYear || year > MaxYear))
            errors["year"] = $"Year must be 0 (all-time) or between {MinYear} and {MaxYear}";

        if (month.HasValue && (month.Value < 1 || month.Value > 12))
            errors["month"] = "Month must be between 1 and 12";

        if (month.HasValue && year == 0)
            errors["year"] = "A monthly period requires a concrete year";

        if (errors.Count > 0)
            throw new HttpBadRequestException(errors, "Invalid period");
    }

    #endregion
}
