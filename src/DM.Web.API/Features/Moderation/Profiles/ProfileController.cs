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

    /// <summary>
    /// Remove a user's account
    /// </summary>
    /// <remarks>
    /// The way an account is removed on the site. There is deliberately no
    /// screen for the owner of an account to do it himself: a reader who wants
    /// his account gone writes to support, and a senior moderator does it here.
    ///
    /// The removal is the soft one. The row stays, so the address and the name
    /// stay reserved and cannot be taken by a new registration, and everything
    /// the user wrote stays where it is without his name on it - it is woven into
    /// other people's topics, games and conversations, and pulling it out would
    /// tear them.
    ///
    /// Reach: a senior moderator, never over an administrator and never over his
    /// own rank.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <param name="request">Why the account is being removed</param>
    /// <response code="204">Account removed</response>
    /// <response code="400">No reason given</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">SeniorModerator or higher role required, and the target must rank below the caller</response>
    /// <response code="404">User not found</response>
    [HttpDelete("{username}/account", Name = nameof(RemoveAccount))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAccount(
        string username, [FromBody] RemoveAccountRequest request)
    {
        await _profileApiService.RemoveAccount(username, request);
        return NoContent();
    }

    /// <summary>
    /// Bring a removed account back
    /// </summary>
    /// <remarks>
    /// The mirror of the removal above, on the same address and by the same rank,
    /// and the whole of what restoration is here: a reader who wants his account
    /// back writes to support like he would about anything else, a person reads
    /// that letter, and the account answers again the moment he acts. There is no
    /// request to file and nothing to wait for.
    ///
    /// Reach: a senior moderator, over any removed account. The removal refuses an
    /// equal rank because taking an account from an equal is an act against him;
    /// giving one back is not, and the same refusal here would leave a moderator's
    /// own removed account with nobody able to bring it back.
    ///
    /// A name that no removed account carries is a 404, a live account under that
    /// name included: there is nothing here to restore either way.
    ///
    /// A removed account named "Аккаунт удален", or a spelling a reader would take
    /// for it, is a 409: that name signs everything removed accounts wrote and is the
    /// address of the profile gathering it, and no live account may hold it.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <response code="204">Account restored</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">SeniorModerator or higher role required</response>
    /// <response code="404">No removed account carries this name</response>
    /// <response code="409">The account carries the reserved name "Аккаунт удален"</response>
    [HttpPost("{username}/account", Name = nameof(RestoreAccount))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreAccount(string username)
    {
        await _profileApiService.RestoreAccount(username);
        return NoContent();
    }

    /// <summary>
    /// List the removed accounts
    /// </summary>
    /// <remarks>
    /// The one list that can see a removed account at all. Such a row is hidden
    /// from every other list, profile and lookup in the product, so a moderator
    /// holding a support letter has no other way to find the account it is about -
    /// or to see that the account was never removed in the first place.
    ///
    /// `search` matches a name or an address, because whoever wrote the letter is
    /// the one who decided which of the two he quoted. A term nothing matches is an
    /// empty page rather than a refusal.
    ///
    /// The address is an administrator's to read, as it is on the moderated
    /// profile; a senior moderator gets the same page with the addresses left out.
    /// </remarks>
    /// <param name="query">Search term and page bounds</param>
    /// <response code="200">Page of removed accounts</response>
    /// <response code="400">Invalid pagination parameters</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">SeniorModerator or higher role required</response>
    [HttpGet("removed", Name = nameof(GetRemovedAccounts))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(typeof(ListEnvelope<RemovedAccount>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRemovedAccounts([FromQuery] RemovedAccountsQuery query) =>
        Ok(await _profileApiService.GetRemovedAccounts(query));
}
