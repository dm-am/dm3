using System;
using System.Threading.Tasks;
using DM.Web.API.Swagger;
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
/// - Each warning carries 0-6 points (0 = verbal warning, no points)
/// - A warning is never withdrawn; its points can be changed, and the total of a
///   user counts the points as they stand now
/// - Reaching the threshold (WarningPolicy) puts the site's own two-day ban on the
///   user, the ordinary scope and nothing heavier; every other ban is a senior
///   moderator's decision, and so is what happens to the automatic one
/// </remarks>
[ApiController]
[Route("v1/moderation/warnings")]
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
    /// Returns every warning the specified user has, with the total of the points
    /// as they stand now.
    /// Public view: points and dates only, without warning reason
    /// and moderator identity.
    /// </remarks>
    /// <param name="username">Username</param>
    /// <response code="200">User warnings info</response>
    // No 404 is declared: an unknown name is answered with an empty list, the way
    // the service has always answered it, and a contract promising otherwise
    // described a refusal nothing here throws.
    [HttpGet("~/v1/users/{username}/warnings", Name = nameof(GetUserWarnings))]
    [ProducesResponseType(typeof(UserWarningsInfo), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserWarnings(string username) =>
        Ok(await _warningApiService.GetUserWarnings(username));

    /// <summary>
    /// Get the warning log
    /// </summary>
    /// <remarks>
    /// Open to everyone: the log the rules page links to. Returns every warning
    /// across the website, newest first, a page at a time; `username` narrows it to
    /// one warned reader, and a name nobody carries answers with an empty page.
    ///
    /// A row carries the warned reader, the moderator, the points, the reason and
    /// the moment, plus the second signature when the points were changed: who
    /// changed them, when, why, and what the warning was issued with. It carries
    /// what the warning was given for as well: the offending text as it stood at
    /// the moment of issue, with its place, or for a warning on a profile the
    /// status, "О себе" and the avatar of that moment, the avatar until moderation
    /// deletes it. The address of the text is given only where a guest could
    /// follow it. The identifier of the object, the address a guest could not
    /// follow, the full cards of both users and whether the caller may change the
    /// points are moderation's and stay in GET /v1/moderation/warnings.
    /// </remarks>
    /// <param name="q">Username filter and page bounds</param>
    /// <response code="200">Page of the warning log</response>
    /// <response code="400">Invalid filter or pagination parameters</response>
    [HttpGet("~/v1/warnings", Name = nameof(GetWarningLog))]
    [ProducesResponseType(typeof(ListEnvelope<WarningLogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetWarningLog([FromQuery] WarningsQuery q) =>
        Ok(await _warningApiService.GetWarningLog(q));

    /// <summary>
    /// Get all warnings (moderators only)
    /// </summary>
    /// <remarks>
    /// The same log as GET /v1/warnings, with the evidence of each row and behind
    /// the moderator role. Paged: the screen above it is titled "latest warnings",
    /// and nothing about a warning bounds how many there are.
    /// </remarks>
    /// <param name="q">Username filter and page bounds</param>
    /// <response code="200">Page of warnings</response>
    /// <response code="400">Invalid filter or pagination parameters</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet(Name = nameof(GetAllWarnings))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<Warning>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    // A username filter is `username` on the wire, per the query vocabulary in
    // API_DESIGN.md; this one alone said `user`.
    public async Task<IActionResult> GetAllWarnings([FromQuery] WarningsQuery q) =>
        Ok(await _warningApiService.GetAllWarnings(q));

    /// <summary>
    /// Create a warning
    /// </summary>
    /// <remarks>
    /// Issues a warning to a user. Requires Moderator role.
    ///
    /// **Warning points:**
    /// - 0 points: Verbal warning (recorded, no points)
    /// - 1 point: Minor violation
    /// - 2 points: Moderate violation
    /// - 3-6 points: Serious violation
    ///
    /// A warning that carries the user's total to the threshold puts the site's
    /// own ban on him: the ordinary scope, two days, filed under the system
    /// account, and lifted or replaced by a senior moderator. It fires on the
    /// crossing, never over a ban already running.
    /// </remarks>
    /// <param name="request">Warning details</param>
    /// <response code="201">Warning created</response>
    /// <response code="400">Invalid warning data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    /// <response code="404">Target user not found</response>
    [HttpPost(Name = nameof(CreateWarning))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(Envelope<Warning>), StatusCodes.Status201Created)]
    [CreatedWithoutLocation]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateWarning([FromBody] CreateWarningRequest request)
    {
        var result = await _warningApiService.CreateWarning(request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Change the points of a warning
    /// </summary>
    /// <remarks>
    /// The warning stays where it is: it is the record of a decision and there is
    /// no way to withdraw it. Only the points move, and the change is signed by
    /// whoever made it and carries his explanation, which is shown wherever the
    /// warning is shown, the open log included.
    ///
    /// A senior moderator may change the points of any warning; a moderator, only
    /// of the ones he issued himself. Points at zero mean the warning stops
    /// counting towards the user's total while staying in the log.
    ///
    /// Nothing is lifted by that on its own: a ban already issued stays until a
    /// senior moderator lifts it, whoever issued it. Raising the points can work
    /// the other way, though - a correction that carries the total to the
    /// threshold puts the site's own two-day ban on the user exactly as a fresh
    /// warning would.
    /// </remarks>
    /// <param name="id">Warning identifier</param>
    /// <param name="request">New points and the explanation for them</param>
    /// <response code="200">Warning with the changed points</response>
    /// <response code="400">Points outside 0-6, or no explanation given</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Not a senior moderator and not the author of this warning</response>
    /// <response code="404">Warning not found</response>
    [HttpPatch("{id}", Name = nameof(ChangeWarningPoints))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(Envelope<Warning>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeWarningPoints(
        Guid id, [FromBody] ChangeWarningPointsRequest request) =>
        Ok(await _warningApiService.ChangeWarningPoints(id, request));

    /// <summary>
    /// Take the picture out of a warning on a profile
    /// </summary>
    /// <remarks>
    /// A warning issued on a profile keeps the avatar of that moment, and both
    /// logs show it after its owner has replaced it. A picture that must not be
    /// shown even as evidence is taken out here: the upload is deleted in the name
    /// of the moderator, both logs stop giving it at once, the file goes once the
    /// grace period of deleted uploads is over, and there is no way back. The
    /// status and "О себе" of the warning stay. Requires Moderator role, the right
    /// to delete a file of somebody else.
    ///
    /// Answered with 204 as well when the warning pins no picture, or when
    /// moderation has deleted it already: what was asked for holds, and a second
    /// request changes neither who deleted the picture nor when.
    ///
    /// A warning issued to the caller himself, before his promotion or by a
    /// higher rank, is refused with 403 and nothing is marked: a party does not
    /// rule on his own case, and the picture is his own avatar, which a deletion
    /// in his name would leave in the log and take out of his profile.
    /// </remarks>
    /// <param name="id">Warning identifier</param>
    /// <response code="204">The warning shows no picture</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required, or the warning was issued to the caller</response>
    /// <response code="404">Warning not found</response>
    [HttpDelete("{id}/profile-picture", Name = nameof(DeleteWarningProfilePicture))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWarningProfilePicture(Guid id)
    {
        await _warningApiService.DeleteProfilePicture(id);
        return NoContent();
    }
}
