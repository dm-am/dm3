using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Bans;

/// <summary>
/// Ban management endpoints
/// </summary>
/// <remarks>
/// Provides endpoints for managing user bans.
/// Most operations require Moderator role or higher.
///
/// ## Ban Types
/// - **Auto**: Triggered by warning points (6+ in 30 days)
/// - **Temporary**: Set by moderator with expiration
/// - **Permanent**: Indefinite ban (Admin can lift)
/// - **Voluntary**: Self-requested by user
/// </remarks>
[ApiController]
[Route("v1/bans")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Bans")]
public class BanController : ControllerBase
{
    private readonly IBanApiService _banApiService;

    /// <inheritdoc />
    public BanController(IBanApiService banApiService)
    {
        _banApiService = banApiService;
    }

    /// <summary>
    /// Get ban status for a user
    /// </summary>
    /// <remarks>
    /// Returns current ban status and ban history for the user.
    /// </remarks>
    /// <param name="login">User login</param>
    /// <response code="200">User ban status and history</response>
    /// <response code="404">User not found</response>
    [HttpGet("~/v1/users/{login}/bans", Name = nameof(GetUserBans))]
    [ProducesResponseType(typeof(UserBanStatus), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetUserBans(string login) =>
        Ok(await _banApiService.GetUserBanStatus(login));

    /// <summary>
    /// Get active ban for a user
    /// </summary>
    /// <remarks>
    /// Returns the currently active ban for the user, or 404 if not banned.
    /// </remarks>
    /// <param name="login">User login</param>
    /// <response code="200">Active ban details</response>
    /// <response code="404">User not found or not banned</response>
    [HttpGet("~/v1/users/{login}/bans/active", Name = nameof(GetActiveBan))]
    [ProducesResponseType(typeof(Envelope<Ban>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetActiveBan(string login)
    {
        var ban = await _banApiService.GetActiveBan(login);
        if (ban == null)
        {
            return NotFound(new GeneralError("User is not currently banned"));
        }
        return Ok(ban);
    }

    /// <summary>
    /// Get all active bans (moderators only)
    /// </summary>
    /// <remarks>
    /// Returns all currently active bans across the platform.
    /// </remarks>
    /// <param name="type">Optional ban type filter</param>
    /// <response code="200">List of active bans</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet(Name = nameof(GetAllBans))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<Ban>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    public async Task<IActionResult> GetAllBans([FromQuery] BanType? type = null) =>
        Ok(await _banApiService.GetAllActiveBans(type));

    /// <summary>
    /// Create a ban
    /// </summary>
    /// <remarks>
    /// Bans a user. Requires Moderator role.
    ///
    /// **Duration options:**
    /// - Set `expiresUtc` for specific end time
    /// - Set `durationHours` for relative duration
    /// - Leave both null for permanent ban
    ///
    /// **Note:** Permanent bans can only be lifted by Admin.
    /// </remarks>
    /// <param name="request">Ban details</param>
    /// <response code="201">Ban created</response>
    /// <response code="400">Invalid ban data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    /// <response code="404">Target user not found</response>
    /// <response code="409">User is already banned</response>
    [HttpPost(Name = nameof(CreateBan))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(Envelope<Ban>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    public async Task<IActionResult> CreateBan([FromBody] CreateBanRequest request)
    {
        var result = await _banApiService.CreateBan(request);
        return CreatedAtRoute(nameof(GetUserBans), new { login = request.UserLogin }, result);
    }

    /// <summary>
    /// Lift (cancel) a ban early
    /// </summary>
    /// <remarks>
    /// Cancels an active ban before its expiration.
    /// Requires Moderator role for temporary bans, Admin for permanent bans.
    /// </remarks>
    /// <param name="id">Ban identifier</param>
    /// <param name="request">Optional lift reason</param>
    /// <response code="204">Ban lifted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator/Admin role required</response>
    /// <response code="404">Ban not found</response>
    [HttpDelete("{id}", Name = nameof(LiftBan))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> LiftBan(Guid id, [FromBody] LiftBanRequest? request = null)
    {
        await _banApiService.LiftBan(id, request);
        return NoContent();
    }
}
