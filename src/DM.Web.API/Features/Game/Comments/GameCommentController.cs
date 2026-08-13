using DM.Web.API.Shared.Authentication;
using DM.Domain.Core.Comments;
using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Comments;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Games;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Game.Comments;

/// <summary>
/// Game comment management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for out-of-character comments on games.
/// Comments support likes and are separate from in-character posts.
/// </remarks>
[ApiController]
[Route("v1/games")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Comments")]
public class GameCommentController : ControllerBase
{
    private readonly IGameCommentApiService _commentApiService;
    private readonly IGameCommentLikeApiService _likeApiService;
    private readonly IGameApiService _gameApiService;

    /// <summary>
    /// Creates a new instance of GameCommentController
    /// </summary>
    public GameCommentController(
        IGameCommentApiService commentApiService,
        IGameCommentLikeApiService likeApiService,
        IGameApiService gameApiService)
    {
        _commentApiService = commentApiService;
        _likeApiService = likeApiService;
        _gameApiService = gameApiService;
    }

    /// <summary>
    /// Get list of comments in game
    /// </summary>
    /// <remarks>
    /// Returns paginated list of out-of-character comments in the game.
    /// Supports filtering by author usernames, text search, date range and sorting.
    ///
    /// ## Query Parameters
    /// - **skip**: Number of items to skip (pagination)
    /// - **take**: Number of items to return (max 100, default 20)
    /// - **search**: Text search in comment content (case-insensitive)
    /// - **authorUsernames**: Filter by author usernames, repeated per author (`authorUsernames=alice&amp;authorUsernames=bob`), OR logic
    /// - **createdFromUtc**: Filter by creation date start (ISO 8601)
    /// - **createdToUtc**: Filter by creation date end (ISO 8601)
    /// - **sortBy**: Sort field - "created" (default) or "likes"
    /// - **sortOrder**: Sort direction - "asc" (default for created) or "desc"
    /// </remarks>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="q">Query parameters with filtering, sorting and pagination</param>
    /// <response code="200">Paginated list of comments</response>
    /// <response code="404">Game not found</response>
    [HttpGet("{id}/comments", Name = nameof(GetGameComments))]
    [ProducesResponseType(typeof(ListEnvelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameComments(string id, [FromQuery] CommentsQuery q)
    {
        var gameId = await _gameApiService.ResolveId(id);
        var (comments, paging) = await _commentApiService.Get(gameId, q);
        return Ok(new ListEnvelope<Comment>(comments, paging));
    }

    /// <summary>
    /// Create new comment in game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="request">Comment creation request</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of comment properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create a comment in this game</response>
    /// <response code="404">Game not found</response>
    [HttpPost("{id}/comments", Name = nameof(PostGameComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostGameComment(string id, [FromBody] CreateCommentRequest request)
    {
        var gameId = await _gameApiService.ResolveId(id);
        var result = await _commentApiService.Create(gameId, request);
        return CreatedAtRoute(nameof(GetGameComment), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get game comment
    /// </summary>
    /// <param name="id">Comment identifier</param>
    /// <response code="200">Returns the comment details</response>
    /// <response code="404">Comment not found</response>
    [HttpGet("comments/{id}", Name = nameof(GetGameComment))]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameComment(Guid id) => Ok(await _commentApiService.Get(id));

    /// <summary>
    /// Update game comment
    /// </summary>
    /// <param name="id">Comment identifier</param>
    /// <param name="request">Updated comment text</param>
    /// <response code="200">Returns the updated comment</response>
    /// <response code="400">Some changed comment properties were invalid or passed id was not recognized</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to change this comment</response>
    /// <response code="404">Comment not found</response>
    [HttpPatch("comments/{id}", Name = nameof(PatchGameComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchGameComment(Guid id, [FromBody] UpdateCommentRequest request) =>
        Ok(await _commentApiService.Update(id, request));

    /// <summary>
    /// Delete game comment
    /// </summary>
    /// <param name="id">Comment identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to change this comment</response>
    /// <response code="404">Comment not found</response>
    [HttpDelete("comments/{id}", Name = nameof(DeleteGameComment))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGameComment(Guid id)
    {
        await _commentApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Add new like to game comment
    /// </summary>
    /// <param name="id">Comment identifier</param>
    /// <response code="200">Like added, returns user who liked</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to like the comment</response>
    /// <response code="409">User already liked this comment</response>
    /// <response code="404">Comment not found</response>
    [HttpPost("comments/{id}/likes", Name = nameof(PostGameCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostGameCommentLike(Guid id) =>
        Ok(await _likeApiService.LikeComment(id));

    /// <summary>
    /// Delete like from game comment
    /// </summary>
    /// <param name="id">Comment identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove like from this comment</response>
    /// <response code="409">User has no like for this comment</response>
    /// <response code="404">Comment not found</response>
    [HttpDelete("comments/{id}/likes", Name = nameof(DeleteGameCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGameCommentLike(Guid id)
    {
        await _likeApiService.UnlikeComment(id);
        return NoContent();
    }

    /// <summary>
    /// Mark all game comments as read
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Game not found</response>
    [HttpDelete("{id}/comments/unread", Name = nameof(MarkGameCommentsAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkGameCommentsAsRead(string id)
    {
        var gameId = await _gameApiService.ResolveId(id);
        await _commentApiService.MarkAsRead(gameId);
        return NoContent();
    }
}
