using DM.Web.API.Shared.Authentication;
using DM.Domain.Core.Comments;
using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Games;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CommentRequest = DM.Web.API.Shared.Dto.CommentRequest;

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
    private readonly GameCommentApiService _commentApiService;
    private readonly GameCommentLikeApiService _likeApiService;
    private readonly GameApiService _gameApiService;

    public GameCommentController(
        GameCommentApiService commentApiService,
        GameCommentLikeApiService likeApiService,
        GameApiService gameApiService)
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
        return Ok(await _commentApiService.Get(gameId, q));
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
    public async Task<IActionResult> PostGameComment(string id, [FromBody] CommentRequest request)
    {
        var gameId = await _gameApiService.ResolveId(id);
        var result = await _commentApiService.Create(gameId, request);
        return CreatedAtRoute(nameof(GetGameComment), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get game comment
    /// </summary>
    /// <remarks>
    /// Answers the user the way the game's comment listing does.
    /// </remarks>
    /// <param name="id">Comment identifier</param>
    /// <response code="200">Returns the comment details</response>
    /// <response code="404">Comment not found, or it is not a comment of a game the user may open</response>
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
    public async Task<IActionResult> PatchGameComment(Guid id, [FromBody] CommentRequest request) =>
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
