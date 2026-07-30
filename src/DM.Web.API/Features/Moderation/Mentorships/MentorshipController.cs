using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Mentorships;

/// <summary>
/// Mentorship management endpoints
/// </summary>
/// <remarks>
/// Provides endpoints for premoderation mentorship.
/// Mentors can self-assign to games and blogs to provide content oversight.
/// Requires Mentor role or higher.
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Mentorship")]
[RequireRole(UserRole.Mentor)]
public class MentorshipController : ControllerBase
{
    private readonly IMentorshipApiService _mentorshipApiService;

    /// <inheritdoc />
    public MentorshipController(IMentorshipApiService mentorshipApiService)
    {
        _mentorshipApiService = mentorshipApiService;
    }

    /// <summary>
    /// Assign yourself as mentor for a game
    /// </summary>
    /// <remarks>
    /// Self-assigns the current user as the premoderation mentor for the specified game.
    /// Only one mentor can be assigned per game at a time.
    /// </remarks>
    /// <param name="gameId">Game identifier</param>
    /// <response code="204">Mentor assigned successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Mentor role required</response>
    /// <response code="404">Game not found</response>
    /// <response code="409">Game already has a mentor</response>
    [HttpPost("games/{gameId:guid}/mentor", Name = nameof(AssignGameMentor))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignGameMentor(Guid gameId)
    {
        await _mentorshipApiService.AssignGameMentor(gameId);
        return NoContent();
    }

    /// <summary>
    /// Remove yourself as mentor from a game
    /// </summary>
    /// <remarks>
    /// Removes the current user as the premoderation mentor from the specified game.
    /// Only the assigned mentor can remove themselves.
    /// </remarks>
    /// <param name="gameId">Game identifier</param>
    /// <response code="204">Mentor removed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">You are not the mentor of this game</response>
    /// <response code="404">Game not found</response>
    [HttpDelete("games/{gameId:guid}/mentor", Name = nameof(RemoveGameMentor))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveGameMentor(Guid gameId)
    {
        await _mentorshipApiService.RemoveGameMentor(gameId);
        return NoContent();
    }

    /// <summary>
    /// Assign yourself as mentor for a blog
    /// </summary>
    /// <remarks>
    /// Self-assigns the current user as the premoderation mentor for the specified blog.
    /// Only one mentor can be assigned per blog at a time.
    /// </remarks>
    /// <param name="blogId">Blog identifier</param>
    /// <response code="204">Mentor assigned successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Mentor role required</response>
    /// <response code="404">Blog not found</response>
    /// <response code="409">Blog already has a mentor</response>
    [HttpPost("blogs/{blogId:guid}/mentor", Name = nameof(AssignBlogMentor))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignBlogMentor(Guid blogId)
    {
        await _mentorshipApiService.AssignBlogMentor(blogId);
        return NoContent();
    }

    /// <summary>
    /// Remove yourself as mentor from a blog
    /// </summary>
    /// <remarks>
    /// Removes the current user as the premoderation mentor from the specified blog.
    /// Only the assigned mentor can remove themselves.
    /// </remarks>
    /// <param name="blogId">Blog identifier</param>
    /// <response code="204">Mentor removed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">You are not the mentor of this blog</response>
    /// <response code="404">Blog not found</response>
    [HttpDelete("blogs/{blogId:guid}/mentor", Name = nameof(RemoveBlogMentor))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveBlogMentor(Guid blogId)
    {
        await _mentorshipApiService.RemoveBlogMentor(blogId);
        return NoContent();
    }
}
