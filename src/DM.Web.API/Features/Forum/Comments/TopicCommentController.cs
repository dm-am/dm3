using DM.Web.API.Shared.Authentication;
using System;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Comments;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Forum.Likes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Forum.Comments;

/// <summary>
/// Topic comment management endpoints
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
[Tags("Comments")]
public class TopicCommentController : ControllerBase
{
    private readonly ITopicCommentApiService _commentApiService;
    private readonly ITopicLikeApiService _likeApiService;

    /// <summary>
    /// Creates a new instance of TopicCommentController
    /// </summary>
    public TopicCommentController(
        ITopicCommentApiService commentApiService,
        ITopicLikeApiService likeApiService)
    {
        _commentApiService = commentApiService;
        _likeApiService = likeApiService;
    }

    /// <summary>
    /// Get list of comments in topic
    /// </summary>
    /// <remarks>
    /// Returns paginated list of comments in the specified topic.
    /// Supports filtering by authors, text search, date range and sorting.
    /// Marks comments as read for authenticated users.
    ///
    /// ## Query Parameters
    /// - **skip**: Number of items to skip (pagination)
    /// - **take**: Number of items to return (max 100, default 20)
    /// - **search**: Text search in comment content (case-insensitive)
    /// - **authors**: Filter by author usernames (comma-separated, OR logic)
    /// - **createdFromUtc**: Filter by creation date start (ISO 8601)
    /// - **createdToUtc**: Filter by creation date end (ISO 8601)
    /// - **sortBy**: Sort field - "created" (default) or "likes"
    /// - **sortOrder**: Sort direction - "asc" (default for created) or "desc"
    /// </remarks>
    /// <param name="id">Topic identifier (GUID)</param>
    /// <param name="q">Query parameters with filtering, sorting and pagination</param>
    /// <response code="200">Paginated list of comments</response>
    /// <response code="404">Topic not found</response>
    [HttpGet("~/v1/topics/{id}/comments", Name = nameof(GetTopicComments))]
    [ProducesResponseType(typeof(ListEnvelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTopicComments(Guid id, [FromQuery] CommentsQuery q)
    {
        var (comments, paging) = await _commentApiService.Get(id, q);
        return Ok(new ListEnvelope<Comment>(comments, paging));
    }

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
    /// <response code="404">Topic not found</response>
    [HttpPost("~/v1/topics/{id}/comments", Name = nameof(PostTopicComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostTopicComment(Guid id, [FromBody] CreateCommentRequest request)
    {
        var result = await _commentApiService.Create(id, request);
        return CreatedAtRoute(nameof(GetTopicComment), new { id = result.Resource.Id }, result);
    }


    /// <summary>
    /// Get topic comment
    /// </summary>
    /// <remarks>
    /// Returns detailed information about a specific comment including:
    /// - Comment text and author
    /// - Creation and last edit timestamps
    /// - Like count
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="200">Comment details</response>
    /// <response code="404">Comment not found</response>
    [HttpGet("{id}", Name = nameof(GetTopicComment))]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTopicComment(Guid id) => Ok(await _commentApiService.Get(id));

    /// <summary>
    /// Update topic comment
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
    /// <response code="404">Comment not found</response>
    [HttpPatch("{id}", Name = nameof(PatchTopicComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchTopicComment(Guid id, [FromBody] Comment comment) =>
        Ok(await _commentApiService.Update(id, comment));

    /// <summary>
    /// Delete topic comment
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
    /// <response code="404">Comment not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteTopicComment))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTopicComment(Guid id)
    {
        await _commentApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Like a topic comment
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
    /// <response code="404">Comment not found</response>
    [HttpPost("{id}/likes", Name = nameof(PostTopicCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostTopicCommentLike(Guid id)
    {
        var result = await _likeApiService.LikeComment(id);
        return CreatedAtRoute(nameof(GetTopicComment), new {id}, result);
    }

    /// <summary>
    /// Remove like from topic comment
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
    /// <response code="404">Comment not found</response>
    [HttpDelete("{id}/likes", Name = nameof(DeleteTopicCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTopicCommentLike(Guid id)
    {
        await _likeApiService.UnlikeComment(id);
        return NoContent();
    }

}
