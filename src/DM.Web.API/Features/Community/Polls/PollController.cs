using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Polls;

/// <summary>
/// API controller for managing community polls
/// </summary>
/// <remarks>
/// Polls allow users to create community votes with multiple options.
/// Only moderators and admins can create, update, or delete polls.
/// Authenticated users can vote on active polls.
/// </remarks>
[ApiController]
[Route("v1/polls")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Polls")]
public class PollController : ControllerBase
{
    private readonly IPollApiService _apiService;

    /// <inheritdoc />
    public PollController(
        IPollApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get list of global polls
    /// </summary>
    /// <remarks>
    /// Returns a paginated list of community polls. Polls can be filtered by status.
    /// </remarks>
    /// <param name="q">Query parameters for filtering and pagination</param>
    /// <response code="200">List of polls retrieved successfully</response>
    [HttpGet(Name = nameof(GetPolls))]
    [ProducesResponseType(typeof(ListEnvelope<Poll>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolls([FromQuery] PollsQuery q) => Ok(await _apiService.Get(q));

    /// <summary>
    /// Create new global poll
    /// </summary>
    /// <remarks>
    /// Requires moderator or admin role. Poll must have at least 2 options.
    /// </remarks>
    /// <param name="request">Poll creation request with options</param>
    /// <response code="201">Poll created successfully</response>
    /// <response code="400">Some poll properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create polls</response>
    [HttpPost(Name = nameof(PostPoll))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(typeof(Envelope<Poll>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PostPoll([FromBody] CreatePollRequest request)
    {
        var result = await _apiService.Create(request);
        return CreatedAtRoute(nameof(GetPoll), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get poll by ID
    /// </summary>
    /// <param name="id">Poll identifier</param>
    /// <response code="200">Poll retrieved successfully</response>
    /// <response code="404">Poll not found or was deleted</response>
    [HttpGet("{id}", Name = nameof(GetPoll))]
    [ProducesResponseType(typeof(Envelope<Poll>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPoll(Guid id) => Ok(await _apiService.Get(id));

    /// <summary>
    /// Update poll
    /// </summary>
    /// <param name="id">Poll unique identifier</param>
    /// <param name="request">Poll update request</param>
    /// <response code="200">Poll updated successfully</response>
    /// <response code="400">Invalid update request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to update polls</response>
    /// <response code="404">Poll not found</response>
    [HttpPatch("{id}", Name = nameof(PatchPoll))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(typeof(Envelope<Poll>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchPoll(Guid id, [FromBody] UpdatePollRequest request) =>
        Ok(await _apiService.Update(id, request));

    /// <summary>
    /// Delete poll (soft delete)
    /// </summary>
    /// <param name="id">Poll unique identifier</param>
    /// <response code="204">Poll deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to delete polls</response>
    /// <response code="404">Poll not found</response>
    [HttpDelete("{id}", Name = nameof(DeletePoll))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePoll(Guid id)
    {
        await _apiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Vote for the poll option
    /// </summary>
    /// <param name="id">Poll unique identifier</param>
    /// <param name="optionId">Option ID to vote for</param>
    /// <response code="200">Vote recorded successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to vote for this poll</response>
    /// <response code="404">Poll not found</response>
    [HttpPost("{id}/vote", Name = nameof(PostPollVote))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Poll>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostPollVote(Guid id, [FromQuery] Guid optionId) =>
        Ok(await _apiService.Vote(id, optionId));

    /// <summary>
    /// Delete vote for the poll option
    /// </summary>
    /// <param name="id">Poll unique identifier</param>
    /// <response code="200">Vote removed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to vote for this poll</response>
    /// <response code="404">Poll not found</response>
    [HttpDelete("{id}/vote", Name = nameof(DeletePollVote))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Poll>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePollVote(Guid id) => Ok(await _apiService.Unvote(id));
}
