using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Shared;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Game;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Game;

/// <inheritdoc />
[ApiController]
[Route("v1/games")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Game Comments")]
public class CommentController : ControllerBase
{
    private readonly ICommentApiService _commentApiService;
    private readonly ILikeApiService _likeApiService;

    /// <inheritdoc />
    public CommentController(
        ICommentApiService commentApiService,
        ILikeApiService likeApiService)
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
    [ProducesResponseType(typeof(ListEnvelope<Comment>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetGameComments(Guid id, [FromQuery] PagingQuery q) =>
        Ok(await _commentApiService.Get(id, q));

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
    [ProducesResponseType(typeof(Envelope<Comment>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    [ProducesResponseType(typeof(Envelope<Comment>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    [ProducesResponseType(typeof(Envelope<Comment>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    [ProducesResponseType(typeof(Envelope<User>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteGameCommentLike(Guid id)
    {
        await _likeApiService.DislikeComment(id);
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
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> MarkGameCommentsAsRead(Guid id)
    {
        await _commentApiService.MarkAsRead(id);
        return NoContent();
    }
}