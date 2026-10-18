using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using DM.Domain.Core.Exceptions;

namespace DM.Web.API.Features.Moderation.Bans;

/// <summary>
/// Ban management endpoints
/// </summary>
/// <remarks>
/// Provides endpoints for managing user bans.
/// Viewing ban lists requires Moderator role or higher;
/// creating and lifting bans requires SeniorModerator role or higher.
///
/// ## Ban Types
/// - **Temporary**: Set by moderator with expiration
/// - **Permanent**: Indefinite ban (only Admin can lift)
/// - **Voluntary**: Self-requested by user
///
/// ## The automatic ban
/// The site issues one of its own when a reader's active warning points reach
/// the threshold (WarningPolicy): the ordinary scope, 48 hours, filed under the
/// system account. It reads back as Temporary, because that is what it is - the
/// window exists only to hold the reader at arm's length until a senior
/// moderator or an administrator looks at the case and either lifts it or issues
/// a ban of his own. It is never issued over a ban already running.
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
    /// Public view: ban type and period only, without ban reason
    /// and moderator identity.
    /// </remarks>
    /// <param name="username">Username</param>
    /// <response code="200">User ban status and history</response>
    // No 404 is declared: a name nobody carries is answered with an empty
    // history and "not banned", the way the service has always answered it, so
    // the contract that promised a refusal described one nothing here throws.
    [HttpGet("~/v1/users/{username}/bans", Name = nameof(GetUserBans))]
    [ProducesResponseType(typeof(PublicUserBanStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserBans(string username) =>
        Ok(await _banApiService.GetPublicUserBanStatus(username));

    /// <summary>
    /// Get active ban for a user
    /// </summary>
    /// <remarks>
    /// Returns the currently active ban for the user, or 404 if not banned.
    /// Public view: ban type and period only, without ban reason
    /// and moderator identity.
    /// </remarks>
    /// <param name="username">Username</param>
    /// <response code="200">Active ban details</response>
    /// <response code="404">User not found or not banned</response>
    [HttpGet("~/v1/users/{username}/bans/active", Name = nameof(GetActiveBan))]
    [ProducesResponseType(typeof(Envelope<PublicBan>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveBan(string username)
    {
        var ban = await _banApiService.GetActiveBan(username);
        if (ban == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Пользователь не забанен");
        }
        return Ok(ban);
    }

    /// <summary>
    /// Get all active bans (moderators only)
    /// </summary>
    /// <remarks>
    /// Returns all currently active bans across the website.
    /// </remarks>
    /// <param name="type">Optional ban type filter</param>
    /// <response code="200">List of active bans</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet(Name = nameof(GetAllBans))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<Ban>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllBans([FromQuery] BanType? type = null) =>
        Ok(await _banApiService.GetAllActiveBans(type));

    /// <summary>
    /// Get the ban list (moderators only)
    /// </summary>
    /// <remarks>
    /// The ban log, written the way DM2's bot wrote it: every issue and every
    /// lift is an entry of its own (`kind` Issued or Lifted) at the moment it
    /// happened (`occurredUtc`: the start of the ban, or the moment of the lift),
    /// newest first, a lift before an issue of the same moment. Each entry carries
    /// the ban as it stands now, so a lifted ban appears twice: its issue, and its
    /// lift above every entry that happened before it. Paged by entries, and
    /// `paging.total` counts entries, not bans. `username` narrows the list to
    /// one banned reader; a name nobody carries answers with an empty page.
    /// Every entry carries its `number` in the whole list, counted from the
    /// oldest, which never changes; `entry` asks for the page of this list that
    /// holds the entry of that number instead of the page `skip` names, and the
    /// answer's `paging.skip` tells which page it is.
    /// The unpaged GET /v1/bans endpoint keeps returning only currently active bans.
    /// </remarks>
    /// <param name="q">Username filter, page bounds and the entry whose page is asked for</param>
    /// <response code="200">Page of the ban list</response>
    /// <response code="400">Invalid filter or pagination parameters</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet("history", Name = nameof(GetBanHistory))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<BanLogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBanHistory([FromQuery] BansQuery q) =>
        Ok(await _banApiService.GetBanHistory(q));

    /// <summary>
    /// Create a ban
    /// </summary>
    /// <remarks>
    /// Bans a user. Requires SeniorModerator role.
    ///
    /// **Duration options:**
    /// - Set `expiresUtc` for specific end time
    /// - Set `durationHours` for relative duration
    /// - One of the two is required: a permanent ban is sent as a hundred-year duration
    ///
    /// **Note:** Permanent bans can only be lifted by Admin.
    /// </remarks>
    /// <param name="request">Ban details</param>
    /// <response code="201">Ban created</response>
    /// <response code="400">Invalid ban data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">SeniorModerator role required</response>
    /// <response code="404">Target user not found</response>
    /// <response code="409">User is already banned</response>
    [HttpPost(Name = nameof(CreateBan))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(typeof(Envelope<Ban>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBan([FromBody] CreateBanRequest request)
    {
        var result = await _banApiService.CreateBan(request);
        return CreatedAtRoute(nameof(GetActiveBan), new { username = request.Username }, result);
    }

    /// <summary>
    /// Lift (cancel) a ban early
    /// </summary>
    /// <remarks>
    /// Cancels an active ban before its expiration.
    /// Requires SeniorModerator role; permanent bans can only be lifted by Admin,
    /// and nobody lifts a ban of his own. The `canLift` flag of every ban the
    /// lists return is answered by the same rule.
    /// The reason is required and travels in the body: the ban list shows it
    /// in the entry of the lift.
    /// </remarks>
    /// <param name="id">Ban identifier</param>
    /// <param name="request">Why the ban is lifted</param>
    /// <response code="204">Ban lifted</response>
    /// <response code="400">No reason given</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">SeniorModerator role required (Admin for permanent bans), or the ban is the caller's own</response>
    /// <response code="404">Ban not found, or already lifted</response>
    /// <response code="409">The ban has already run out</response>
    [HttpDelete("{id}", Name = nameof(LiftBan))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LiftBan(Guid id, [FromBody] LiftBanRequest request)
    {
        await _banApiService.LiftBan(id, request);
        return NoContent();
    }
}
