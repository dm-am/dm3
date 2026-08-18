using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// User profile moderation endpoints
/// </summary>
/// <remarks>
/// Provides endpoints for viewing and moderating user profiles.
/// Fields are filtered server-side based on the caller's role:
///
/// - **Admin**: all fields (email, IP addresses, login history, linked profiles, notes, violations)
/// - **SeniorModerator**: linked profiles, notes, violations (can issue bans, edit profiles)
/// - **Moderator**: linked profiles, notes, violations (can issue warnings)
///
/// The response includes a `permissions` object that tells the frontend which UI elements to show.
/// </remarks>
[ApiController]
[Route("v1/moderation/users")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Profiles")]
public class ProfileController : ControllerBase
{
    private readonly IModeratedProfileApiService _profileApiService;

    /// <inheritdoc />
    public ProfileController(IModeratedProfileApiService profileApiService)
    {
        _profileApiService = profileApiService;
    }

    /// <summary>
    /// Get moderated profile for a user
    /// </summary>
    /// <remarks>
    /// Returns aggregated moderation data including linked profiles, moderator notes,
    /// violation summary, and (for admins) IP addresses and login history.
    ///
    /// Use the `permissions` field in the response to determine which actions
    /// the current user can perform and which UI sections to display.
    /// </remarks>
    /// <param name="username">Target user's display name</param>
    /// <response code="200">Moderated profile (fields filtered by caller role)</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role or higher required</response>
    /// <response code="404">User not found</response>
    [HttpGet("{username}/profile", Name = nameof(GetModeratedProfile))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ModeratedProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModeratedProfile(string username) =>
        Ok(await _profileApiService.GetModeratedProfile(username));

    /// <summary>
    /// Moderate user profile
    /// </summary>
    /// <remarks>
    /// Allows SeniorModerator or higher to edit user profile Info field.
    /// Used to remove inappropriate content from user profiles.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <param name="profile">Profile moderation data</param>
    /// <response code="200">Profile moderated successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">SeniorModerator or higher role required</response>
    /// <response code="404">User not found</response>
    [HttpPatch("{username}/profile", Name = nameof(ModerateUserProfile))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModerateUserProfile(string username, [FromBody] ModerateProfile profile) =>
        Ok(await _profileApiService.ModerateUserProfile(username, profile));

    /// <summary>
    /// Put a user under moderation watch, or take them off it
    /// </summary>
    /// <remarks>
    /// While the watch is on, every game and blog the user creates starts in
    /// premoderation, the way a newbie's does. Unlike the violators list, which is
    /// recomputed from active warnings and bans and lapses when they expire, this
    /// flag stays until a moderator clears it.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <param name="watch">Whether the watch is on from now on</param>
    /// <response code="200">Watch updated; the moderated profile is returned</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Moderator or higher role required</response>
    /// <response code="404">User not found</response>
    [HttpPatch("{username}/moderation-watch", Name = nameof(SetModerationWatch))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(Envelope<ModeratedProfile>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetModerationWatch(
        string username, [FromBody] ModerationWatchRequest watch) =>
        Ok(await _profileApiService.SetModerationWatch(username, watch.UnderWatch));

    /// <summary>
    /// Set user role
    /// </summary>
    /// <remarks>
    /// Allows Admin to change user's role.
    /// Cannot set Guest role.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <param name="role">New role</param>
    /// <response code="200">Role updated successfully</response>
    /// <response code="400">Invalid role (e.g., Guest)</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Admin role required</response>
    /// <response code="404">User not found</response>
    [HttpPatch("{username}/role/{role}", Name = nameof(SetUserRole))]
    [RequireRole(UserRole.Admin)]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetUserRole(string username, UserRole role) =>
        Ok(await _profileApiService.SetUserRole(username, role));
}
