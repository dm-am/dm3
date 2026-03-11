using DM.Web.API.Shared.Authentication;
using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Game.Comments;

/// <inheritdoc />
[ApiController]
[Route("v1/games")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Comments")]
public class GameCommentController : ControllerBase
{
    private readonly IGameCommentApiService _commentApiService;
    private readonly IGameCommentLikeApiService _likeApiService;

    /// <inheritdoc />
    public GameCommentController(
        IGameCommentApiService commentApiService,
        IGameCommentLikeApiService likeApiService)
    {
        _commentApiService = commentApiService;
        _likeApiService = likeApiService;
    }

    /// <summary>
    /// Get list of comments in game
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">Returns the comment list</response>
    /// <response code="410">Game not found</response>
    [HttpGet("{id}/comments", Name = nameof(GetGameComments))]
    [ProducesResponseType(typeof(ListEnvelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameComments(Guid id, [FromQuery] PagingQuery q)
    {
        var (comments, paging) = await _commentApiService.Get(id, q);
        return Ok(new ListEnvelope<Comment>(comments, paging));
    }

    /// <summary>
    /// Create new comment in game
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <param name="request">Comment creation request</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of comment properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create a comment in this game</response>
    /// <response code="410">Game not found</response>
    [HttpPost("{id}/comments", Name = nameof(PostGameComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Comment), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostGameComment(Guid id, [FromBody] CreateCommentRequest request)
    {
        var result = await _commentApiService.Create(id, request);
        return CreatedAtRoute(nameof(GetGameComment), new {id = result.Resource.Id}, result);
    }

    /// <summary>
    /// Get game comment
    /// </summary>
    /// <param name="id">Comment identifier</param>
    /// <response code="200">Returns the comment details</response>
    /// <response code="410">Comment not found</response>
    [HttpGet("comments/{id}", Name = nameof(GetGameComment))]
    [ProducesResponseType(typeof(Comment), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameComment(Guid id) => Ok(await _commentApiService.Get(id));

    /// <summary>
    /// Update game comment
    /// </summary>
    /// <param name="id">Comment identifier</param>
    /// <param name="comment">Updated comment details</param>
    /// <response code="200">Returns the updated comment</response>
    /// <response code="400">Some changed comment properties were invalid or passed id was not recognized</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to change this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpPatch("comments/{id}", Name = nameof(PatchGameComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Comment), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchGameComment(Guid id, [FromBody] Comment comment) =>
        Ok(await _commentApiService.Update(id, comment));

    /// <summary>
    /// Delete game comment
    /// </summary>
    /// <param name="id">Comment identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to change this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpDelete("comments/{id}", Name = nameof(DeleteGameComment))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGameComment(Guid id)
    {
        await _commentApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Add new like to game comment
    /// </summary>
    /// <param name="id">Comment identifier</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to like the comment</response>
    /// <response code="409">User already liked this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpPost("comments/{id}/likes", Name = nameof(PostGameCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostGameCommentLike(Guid id)
    {
        var result = await _likeApiService.LikeComment(id);
        return CreatedAtRoute(nameof(GetGameComment), new {id}, result);
    }

    /// <summary>
    /// Delete like from game comment
    /// </summary>
    /// <param name="id">Comment identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove like from this comment</response>
    /// <response code="409">User has no like for this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpDelete("comments/{id}/likes", Name = nameof(DeleteGameCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGameCommentLike(Guid id)
    {
        await _likeApiService.UnlikeComment(id);
        return NoContent();
    }

    /// <summary>
    /// Mark all game comments as read
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Game not found</response>
    [HttpDelete("{id}/comments/unread", Name = nameof(MarkGameCommentsAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkGameCommentsAsRead(Guid id)
    {
        await _commentApiService.MarkAsRead(id);
        return NoContent();
    }
}
