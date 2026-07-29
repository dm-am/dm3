using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Violators list endpoints
/// </summary>
/// <remarks>
/// Aggregated moderation view of users with active warning points
/// or an active ban. Requires Moderator role or higher.
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Violators")]
public class ViolatorController : ControllerBase
{
    private readonly IViolatorApiService _violatorApiService;

    /// <inheritdoc />
    public ViolatorController(IViolatorApiService violatorApiService)
    {
        _violatorApiService = violatorApiService;
    }

    /// <summary>
    /// Get violators (moderators only)
    /// </summary>
    /// <remarks>
    /// Returns users having active warning points or an active ban,
    /// sorted by points descending. Each row contains the user reference,
    /// current points (against the 6-point auto-ban threshold) and
    /// active ban details if the user is banned.
    ///
    /// **Filter values:**
    /// - `all` (default): points or active ban
    /// - `banned`: only users with an active ban
    /// - `points-only`: only users with points and no active ban
    /// </remarks>
    /// <param name="query">Filter parameters</param>
    /// <response code="200">List of violators</response>
    /// <response code="400">Invalid filter value</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet("violators", Name = nameof(GetViolators))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<Violator>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetViolators([FromQuery] ViolatorsQuery query) =>
        Ok(await _violatorApiService.GetViolators(query.Filter));
}
