using System.Threading.Tasks;
using DM.Web.API.Dto.Community;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Services.Community;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Community;

/// <summary>
/// Community statistics controller
/// </summary>
[ApiController]
[Route("v1/community")]
[ApiExplorerSettings(GroupName = "Community")]
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
    /// <response code="200">Statistics retrieved successfully</response>
    [HttpGet("stats", Name = nameof(GetCommunityStats))]
    [ProducesResponseType(typeof(Envelope<CommunityStats>), 200)]
    public async Task<IActionResult> GetCommunityStats() =>
        Ok(await _statsService.GetStats());
}
