using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Endorsements;

/// <summary>
/// User endorsements controller - list, create, update, delete endorsements
/// </summary>
[ApiController]
[Route("v1/users/{username}/endorsements")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("User Endorsements")]
public class UserEndorsementController : ControllerBase
{
    private readonly IUserEndorsementApiService _endorsementApiService;

    /// <inheritdoc />
    public UserEndorsementController(IUserEndorsementApiService endorsementApiService)
    {
        _endorsementApiService = endorsementApiService;
    }

    /// <summary>
    /// Get endorsements received by a user
    /// </summary>
    /// <remarks>
    /// Returns endorsements written ABOUT this user
    /// (endorsements where they are the recipient). Supports
    /// substring search over text/author name, sorting by date
    /// or author name, and paging via PagingQuery.
    /// </remarks>
    /// <param name="username">Endorsement recipient's username.</param>
    /// <param name="q">Search / sort / paging.</param>
    /// <response code="200">List of endorsements.</response>
    /// <response code="404">User not found.</response>
    [HttpGet(Name = nameof(GetUserEndorsements))]
    [ProducesResponseType(typeof(ListEnvelope<UserEndorsement>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserEndorsements(string username, [FromQuery] UserEndorsementsQuery q) =>
        Ok(await _endorsementApiService.GetReceived(username, q));

    /// <summary>
    /// Get endorsements written by a user
    /// </summary>
    /// <remarks>
    /// Returns endorsements written BY this user (they are the
    /// author). Symmetric with GET endorsements; the same search/sort/paging
    /// parameters. Used on the "Написанные рекомендации" page
    /// in the profile.
    /// </remarks>
    /// <param name="username">Endorsement author's username.</param>
    /// <param name="q">Search / sort / paging.</param>
    /// <response code="200">List of endorsements written by the user.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("/v1/users/{username}/written-endorsements", Name = nameof(GetWrittenUserEndorsements))]
    [ProducesResponseType(typeof(ListEnvelope<UserEndorsement>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWrittenUserEndorsements(string username, [FromQuery] UserEndorsementsQuery q) =>
        Ok(await _endorsementApiService.GetWritten(username, q));

    /// <summary>
    /// Ask whether the caller may write a recommendation about this user
    /// </summary>
    /// <remarks>
    /// The whole rule set the POST below enforces — signed in, not oneself,
    /// past probation, played in the same game, no recommendation for this
    /// pair yet — evaluated in advance and answered as one flag plus the
    /// refusal sentence. A client draws the "write a recommendation" control
    /// on that flag and therefore never offers what the POST would reject.
    /// A guest is answered 200 with canCreate=false, not 401: "may I?" has an
    /// answer for anonymous readers too.
    /// </remarks>
    /// <param name="username">Prospective recipient's username.</param>
    /// <response code="200">Whether a recommendation may be written, and why not.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("eligibility", Name = nameof(GetUserEndorsementEligibility))]
    [ProducesResponseType(typeof(Envelope<EndorsementEligibility>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserEndorsementEligibility(string username) =>
        Ok(await _endorsementApiService.GetEligibility(username));

    /// <summary>
    /// Create user endorsement
    /// </summary>
    /// <remarks>
    /// Creates a positive endorsement for the specified user.
    /// You can only endorse users you have played with in the same game.
    /// Only one endorsement per user is allowed.
    /// Requires at least 100 game posts to create endorsements.
    /// </remarks>
    /// <param name="username">Username of user to endorse</param>
    /// <param name="request">Endorsement data</param>
    /// <response code="201">Endorsement created successfully</response>
    /// <response code="400">Invalid endorsement data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (haven't played together, endorsing yourself, or newbie)</response>
    /// <response code="404">User not found</response>
    /// <response code="409">Endorsement already exists</response>
    [HttpPost(Name = nameof(CreateUserEndorsement))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(UserEndorsement), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUserEndorsement(
        string username, [FromBody] CreateUserEndorsementRequest request)
    {
        var endorsement = await _endorsementApiService.Create(username, request);
        return CreatedAtRoute(nameof(GetUserEndorsement), new { id = endorsement.Id }, endorsement);
    }

    /// <summary>
    /// Get single endorsement by ID
    /// </summary>
    /// <param name="id">Endorsement identifier</param>
    /// <response code="200">Endorsement details</response>
    /// <response code="404">Endorsement not found</response>
    [HttpGet("/v1/endorsements/{id}", Name = nameof(GetUserEndorsement))]
    [ProducesResponseType(typeof(UserEndorsement), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserEndorsement(Guid id) =>
        Ok(await _endorsementApiService.Get(id));

    /// <summary>
    /// Update user endorsement
    /// </summary>
    /// <remarks>
    /// Updates an existing endorsement.
    /// Only the author can edit their endorsement.
    /// Endorsements can only be edited within 24 hours of creation (admins exempt).
    /// </remarks>
    /// <param name="id">Endorsement identifier</param>
    /// <param name="request">Update data</param>
    /// <response code="200">Endorsement updated successfully</response>
    /// <response code="400">Invalid update data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (not author or edit window expired)</response>
    /// <response code="404">Endorsement not found</response>
    [HttpPatch("/v1/endorsements/{id}", Name = nameof(UpdateUserEndorsement))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(UserEndorsement), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserEndorsement(
        Guid id, [FromBody] UpdateUserEndorsementRequest request) =>
        Ok(await _endorsementApiService.Update(id, request));

    /// <summary>
    /// Delete user endorsement
    /// </summary>
    /// <remarks>
    /// Deletes an existing endorsement.
    /// Can be deleted by the author or a moderator.
    /// </remarks>
    /// <param name="id">Endorsement identifier</param>
    /// <response code="204">Endorsement deleted successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (not author or moderator)</response>
    /// <response code="404">Endorsement not found</response>
    [HttpDelete("/v1/endorsements/{id}", Name = nameof(DeleteUserEndorsement))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUserEndorsement(Guid id)
    {
        await _endorsementApiService.Delete(id);
        return NoContent();
    }
}
