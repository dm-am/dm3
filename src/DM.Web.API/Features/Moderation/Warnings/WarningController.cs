using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Warning management endpoints
/// </summary>
/// <remarks>
/// Provides endpoints for managing user warnings.
/// Most operations require Moderator role or higher.
///
/// ## Warning Points
/// - Each warning carries 1-3 points
/// - 6+ points in 30 days triggers an automatic ban
/// - Points are recalculated when warnings are removed
/// </remarks>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Warnings")]
public class WarningController : ControllerBase
{
    private readonly IWarningApiService _warningApiService;

    /// <inheritdoc />
    public WarningController(IWarningApiService warningApiService)
    {
        _warningApiService = warningApiService;
    }

    /// <summary>
    /// Get warnings for a user
    /// </summary>
    /// <remarks>
    /// Returns all active warnings for the specified user,
    /// including total warning points.
    /// </remarks>
    /// <param name="login">User login</param>
    /// <response code="200">User warnings info</response>
    /// <response code="404">User not found</response>
    [HttpGet("users/{login}/warnings", Name = nameof(GetUserWarnings))]
    [ProducesResponseType(typeof(UserWarningsInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserWarnings(string login) =>
        Ok(await _warningApiService.GetUserWarnings(login));

    /// <summary>
    /// Get all warnings (moderators only)
    /// </summary>
    /// <remarks>
    /// Returns all warnings across the website.
    /// Can be filtered by user login.
    /// </remarks>
    /// <param name="user">Optional user login to filter by</param>
    /// <response code="200">List of warnings</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet("moderation/warnings", Name = nameof(GetAllWarnings))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<Warning>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllWarnings([FromQuery] string? user = null) =>
        Ok(await _warningApiService.GetAllWarnings(user));

    /// <summary>
    /// Create a warning
    /// </summary>
    /// <remarks>
    /// Issues a warning to a user. Requires Moderator role.
    ///
    /// **Warning points:**
    /// - 1 point: Minor violation
    /// - 2 points: Moderate violation
    /// - 3 points: Serious violation
    ///
    /// 6+ points within 30 days triggers automatic ban.
    /// </remarks>
    /// <param name="request">Warning details</param>
    /// <response code="201">Warning created</response>
    /// <response code="400">Invalid warning data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    /// <response code="404">Target user not found</response>
    [HttpPost("warnings", Name = nameof(CreateWarning))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(Envelope<Warning>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateWarning([FromBody] CreateWarningRequest request)
    {
        var result = await _warningApiService.CreateWarning(request);
        return CreatedAtRoute(nameof(GetUserWarnings), new { login = request.Username }, result);
    }

    /// <summary>
    /// Remove (deactivate) a warning
    /// </summary>
    /// <remarks>
    /// Removes a warning from a user. Requires Moderator role.
    /// Warning points are recalculated, which may lift automatic bans.
    /// </remarks>
    /// <param name="id">Warning identifier</param>
    /// <response code="204">Warning removed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    /// <response code="404">Warning not found</response>
    [HttpDelete("warnings/{id}", Name = nameof(RemoveWarning))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveWarning(Guid id)
    {
        await _warningApiService.RemoveWarning(id);
        return NoContent();
    }
}
