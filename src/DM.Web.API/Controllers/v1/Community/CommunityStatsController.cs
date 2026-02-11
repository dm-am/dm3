using System.Threading.Tasks;
using DM.Web.API.Dto.Community;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Services.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Community;

/// <summary>
/// Community statistics controller
/// </summary>
[ApiController]
[Route("v1/stats")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Statistics")]
public class CommunityStatsController : ControllerBase
{
    private readonly ICommunityStatsService _statsService;

    /// <inheritdoc />
    public CommunityStatsController(ICommunityStatsService statsService)
    {
        _statsService = statsService;
    }

    /// <summary>
    /// Get community statistics
    /// </summary>
    /// <remarks>
    /// Returns real-time statistics including online users, totals with today's delta,
    /// last reviewed post, and weekly best post.
    /// </remarks>
    /// <response code="200">Statistics retrieved successfully</response>
    [HttpGet(Name = nameof(GetStats))]
    [ProducesResponseType(typeof(Envelope<LiveStats>), 200)]
    public async Task<IActionResult> GetStats() =>
        Ok(await _statsService.GetLiveStats());
}
