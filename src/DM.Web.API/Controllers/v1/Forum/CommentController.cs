using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Shared;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Boards;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Forum;

/// <summary>
/// Forum comment management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for comments within forum topics.
/// Comments are replies to topics and form the discussion thread.
///
/// ## Available Operations
/// - List comments in a topic (with pagination)
/// - Create, read, update, delete comments
/// - Like/unlike comments
///
/// ## Access Control
/// - Viewing: Same as parent topic's board ViewPolicy
/// - Creating: Authenticated users (if topic is not closed)
/// - Editing: Author or moderators
/// - Deleting: Author or moderators
/// - Liking: Authenticated users (cannot like own comments)
/// </remarks>
[ApiController]
[Route("v1/forum/comments")]
[ApiExplorerSettings(GroupName = "Forum")]
[Tags("Forum Comments")]
public class CommentController : ControllerBase
{
    private readonly ICommentApiService _commentApiService;
    private readonly ILikeApiService _likeApiService;

    /// <summary>
    /// Creates a new instance of CommentController
    /// </summary>
    public CommentController(
        ICommentApiService commentApiService,
        ILikeApiService likeApiService)
    {
        _commentApiService = commentApiService;
        _likeApiService = likeApiService;
    }

    /// <summary>
    /// Get list of comments in topic
    /// </summary>
    /// <remarks>
    /// Returns paginated list of comments in the specified topic.
    /// Comments are sorted by creation date (oldest first).
    /// Marks comments as read for authenticated users.
    /// </remarks>
    /// <param name="id">Topic identifier (GUID)</param>
    /// <param name="q">Pagination parameters (skip, number)</param>
    /// <response code="200">Paginated list of comments</response>
    /// <response code="410">Topic not found</response>
    [HttpGet("~/v1/topics/{id}/comments", Name = nameof(GetForumComments))]
    [ProducesResponseType(typeof(ListEnvelope<Comment>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetForumComments(Guid id, [FromQuery] PagingQuery q) =>
        Ok(await _commentApiService.Get(id, q));

    /// <summary>
    /// Create new comment in topic
    /// </summary>
    /// <remarks>
    /// Adds a new comment to the specified topic.
    /// Cannot add comments to closed topics.
    /// </remarks>
    /// <param name="id">Topic identifier (GUID)</param>
    /// <param name="request">Comment creation request with text content</param>
    /// <response code="201">Comment created successfully</response>
    /// <response code="400">Invalid comment data (empty text, etc.)</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot comment (topic closed or no permission)</response>
    /// <response code="410">Topic not found</response>
    [HttpPost("~/v1/topics/{id}/comments", Name = nameof(PostForumComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PostForumComment(Guid id, [FromBody] CreateCommentRequest request)
    {
        var result = await _commentApiService.Create(id, request);
        return CreatedAtRoute(nameof(CommentController.GetForumComment), new { id = result.Resource.Id }, result);
    }


    /// <summary>
    /// Get forum comment
    /// </summary>
    /// <remarks>
    /// Returns detailed information about a specific comment including:
    /// - Comment text and author
    /// - Creation and last edit timestamps
    /// - Like count
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="200">Comment details</response>
    /// <response code="410">Comment not found</response>
    [HttpGet("{id}", Name = nameof(GetForumComment))]
    [ProducesResponseType(typeof(Envelope<Comment>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetForumComment(Guid id) => Ok(await _commentApiService.Get(id));

    /// <summary>
    /// Update forum comment
    /// </summary>
    /// <remarks>
    /// Updates the comment text.
    /// Only the comment author or moderators can edit comments.
    /// Edit history is preserved.
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <param name="comment">Updated comment data</param>
    /// <response code="200">Updated comment</response>
    /// <response code="400">Invalid comment data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot edit this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpPatch("{id}", Name = nameof(PatchForumComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PatchForumComment(Guid id, [FromBody] Comment comment) =>
        Ok(await _commentApiService.Update(id, comment));

    /// <summary>
    /// Delete forum comment
    /// </summary>
    /// <remarks>
    /// Permanently deletes a comment.
    /// Only the comment author or moderators can delete comments.
    /// This action cannot be undone.
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="204">Comment deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot delete this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteForumComment))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteForumComment(Guid id)
    {
        await _commentApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Like a forum comment
    /// </summary>
    /// <remarks>
    /// Adds a like to the comment from the current user.
    /// Users cannot like their own comments.
    /// Each user can only like a comment once.
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="201">Like added, returns user who liked</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot like this comment (e.g., own comment)</response>
    /// <response code="409">User already liked this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpPost("{id}/likes", Name = nameof(PostForumCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PostForumCommentLike(Guid id)
    {
        var result = await _likeApiService.LikeComment(id);
        return CreatedAtRoute(nameof(GetForumComment), new {id}, result);
    }

    /// <summary>
    /// Remove like from forum comment
    /// </summary>
    /// <remarks>
    /// Removes the current user's like from the comment.
    /// Can only remove your own likes.
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="204">Like removed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot remove like (not their like)</response>
    /// <response code="409">User has not liked this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpDelete("{id}/likes", Name = nameof(DeleteForumCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteForumCommentLike(Guid id)
    {
        await _likeApiService.DislikeComment(id);
        return NoContent();
    }
}