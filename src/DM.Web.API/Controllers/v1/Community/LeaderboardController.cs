using System.Threading.Tasks;
using DM.Web.API.Dto.Community;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Services.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Community;

/// <summary>
/// Controller for community leaderboards
/// </summary>
[ApiController]
[Route("v1/leaderboards")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Statistics")]
public class LeaderboardController : ControllerBase
{
    private readonly ICommunityStatsService _statsService;

    /// <inheritdoc />
    public LeaderboardController(ICommunityStatsService statsService)
    {
        _statsService = statsService;
    }

    /// <summary>
    /// Get yearly leaderboards
    /// </summary>
    /// <param name="year">Year (e.g., 2025)</param>
    /// <response code="200">Yearly leaderboards</response>
    /// <response code="404">Year not found or no data available</response>
    [HttpGet("{year:int}", Name = nameof(GetYearlyLeaderboards))]
    [ProducesResponseType(typeof(Envelope<Leaderboards>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetYearlyLeaderboards(int year) =>
        Ok(await _statsService.GetLeaderboards(year, null));

    /// <summary>
    /// Get monthly leaderboards
    /// </summary>
    /// <param name="year">Year (e.g., 2025)</param>
    /// <param name="month">Month (1-12)</param>
    /// <response code="200">Monthly leaderboards</response>
    /// <response code="404">Period not found or no data available</response>
    [HttpGet("{year:int}/{month:int}", Name = nameof(GetMonthlyLeaderboards))]
    [ProducesResponseType(typeof(Envelope<Leaderboards>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetMonthlyLeaderboards(int year, int month) =>
        Ok(await _statsService.GetLeaderboards(year, month));
}
