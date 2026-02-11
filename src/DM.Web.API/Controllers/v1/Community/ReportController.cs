using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Dto.Community;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Services.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Community;

/// <summary>
/// Controller for community reports
/// </summary>
[ApiController]
[Route("v1/reports")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Statistics")]
public class ReportController : ControllerBase
{
    private readonly ICommunityStatsService _statsService;

    /// <inheritdoc />
    public ReportController(ICommunityStatsService statsService)
    {
        _statsService = statsService;
    }

    /// <summary>
    /// Get yearly report
    /// </summary>
    /// <param name="year">Year (e.g., 2025)</param>
    /// <response code="200">Yearly report</response>
    /// <response code="404">Year not found or no data available</response>
    [HttpGet("{year:int}", Name = nameof(GetYearlyReport))]
    [ProducesResponseType(typeof(Envelope<PeriodReport>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetYearlyReport(int year) =>
        Ok(await _statsService.GetPeriodReport(year, null));

    /// <summary>
    /// Get monthly report
    /// </summary>
    /// <param name="year">Year (e.g., 2025)</param>
    /// <param name="month">Month (1-12)</param>
    /// <response code="200">Monthly report</response>
    /// <response code="404">Period not found or no data available</response>
    [HttpGet("{year:int}/{month:int}", Name = nameof(GetMonthlyReport))]
    [ProducesResponseType(typeof(Envelope<PeriodReport>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetMonthlyReport(int year, int month) =>
        Ok(await _statsService.GetPeriodReport(year, month));

    /// <summary>
    /// Compare two reports
    /// </summary>
    /// <remarks>
    /// Format: "2024,2025" for years or "2025-01,2025-06" for months.
    /// Returns comparison metrics showing growth between the two periods.
    /// </remarks>
    /// <param name="periods">Periods to compare (e.g., "2024,2025" or "2025-01,2025-06")</param>
    /// <response code="200">Report comparison</response>
    /// <response code="400">Invalid period format</response>
    [HttpGet("compare", Name = nameof(CompareReports))]
    [ProducesResponseType(typeof(Envelope<PeriodComparison>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    public async Task<IActionResult> CompareReports([FromQuery] string periods)
    {
        // Parse periods string: "2024,2025" for years or "2025-01,2025-06" for months
        var parts = periods?.Split(',');
        if (parts == null || parts.Length != 2)
        {
            return BadRequest(new BadRequestError("Invalid format", new Dictionary<string, IEnumerable<string>>
            {
                ["periods"] = new[] { "Format: '2024,2025' for years or '2025-01,2025-06' for months" }
            }));
        }

        if (!TryParsePeriod(parts[0].Trim(), out var year1, out var month1) ||
            !TryParsePeriod(parts[1].Trim(), out var year2, out var month2))
        {
            return BadRequest(new BadRequestError("Invalid period format", new Dictionary<string, IEnumerable<string>>
            {
                ["periods"] = new[] { "Use '2024' for year or '2025-01' for month" }
            }));
        }

        return Ok(await _statsService.ComparePeriods(year1, month1, year2, month2));
    }

    private static bool TryParsePeriod(string period, out int year, out int? month)
    {
        year = 0;
        month = null;

        if (period.Contains('-'))
        {
            var periodParts = period.Split('-');
            if (periodParts.Length == 2 &&
                int.TryParse(periodParts[0], out year) &&
                int.TryParse(periodParts[1], out var m) &&
                m >= 1 && m <= 12)
            {
                month = m;
                return true;
            }
            return false;
        }

        return int.TryParse(period, out year) && year >= 1900 && year <= 2100;
    }
}
