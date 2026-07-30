using System;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Blog.Likes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Blog.PublicationComments;

/// <summary>
/// Publication comment management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for comments within blog publications.
/// Comments are replies to publications and form the discussion thread.
///
/// ## Available Operations
/// - List comments in a publication (with pagination)
/// - Create, read, update, delete comments
/// - Like/unlike comments
///
/// ## Access Control
/// - Viewing: Same as parent publication's blog ViewPolicy
/// - Creating: Authenticated users (if publication is not closed)
/// - Editing: Author or moderators
/// - Deleting: Author or moderators
/// - Liking: Authenticated users (cannot like own comments)
/// </remarks>
[ApiController]
[Route("v1/publications")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Publication Comments")]
public class PublicationCommentController : ControllerBase
{
    private readonly IPublicationCommentApiService _commentApiService;
    private readonly IBlogLikeApiService _likeApiService;

    /// <summary>
    /// Creates a new instance of PublicationCommentController
    /// </summary>
    public PublicationCommentController(
        IPublicationCommentApiService commentApiService,
        IBlogLikeApiService likeApiService)
    {
        _commentApiService = commentApiService;
        _likeApiService = likeApiService;
    }

    /// <summary>
    /// Get list of comments in publication
    /// </summary>
    /// <remarks>
    /// Returns paginated list of comments in the specified publication.
    /// Supports filtering by authors, text search, date range and sorting.
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
    /// <param name="id">Publication identifier (GUID)</param>
    /// <param name="q">Query parameters with filtering, sorting and pagination</param>
    /// <response code="200">Paginated list of comments</response>
    /// <response code="404">Publication not found</response>
    [HttpGet("{id}/comments", Name = nameof(GetPublicationComments))]
    [ProducesResponseType(typeof(ListEnvelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicationComments(Guid id, [FromQuery] PublicationCommentsQuery q) =>
        Ok(await _commentApiService.Get(id, q));

    /// <summary>
    /// Create new comment in publication
    /// </summary>
    /// <remarks>
    /// Adds a new comment to the specified publication.
    /// Cannot add comments to closed publications.
    ///
    /// Example request:
    ///     POST /v1/publications/3fa85f64-5717-4562-b3fc-2c963f66afa6/comments
    ///     {
    ///       "text": "Great article! Thanks for sharing."
    ///     }
    /// </remarks>
    /// <param name="id">Publication identifier (GUID)</param>
    /// <param name="request">Comment creation request with text content</param>
    /// <response code="201">Comment created successfully</response>
    /// <response code="400">Invalid comment data (empty text, etc.)</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot comment (publication closed or no permission)</response>
    /// <response code="404">Publication not found</response>
    [HttpPost("{id}/comments", Name = nameof(PostPublicationComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostPublicationComment(Guid id, [FromBody] CreateCommentRequest request)
    {
        var result = await _commentApiService.Create(id, request);
        return CreatedAtRoute(nameof(GetPublicationComment), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get publication comment
    /// </summary>
    /// <remarks>
    /// Returns detailed information about a specific comment including:
    /// - Comment text and author
    /// - Creation and last edit timestamps
    /// - Like count
    ///
    /// Example request:
    ///     GET /v1/publications/comments/3fa85f64-5717-4562-b3fc-2c963f66afa6
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="200">Comment details</response>
    /// <response code="404">Comment not found</response>
    [HttpGet("comments/{id}", Name = nameof(GetPublicationComment))]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicationComment(Guid id) => Ok(await _commentApiService.Get(id));

    /// <summary>
    /// Update publication comment
    /// </summary>
    /// <remarks>
    /// Updates the comment text.
    /// Only the comment author or moderators can edit comments.
    /// Edit history is preserved.
    ///
    /// Example request:
    ///     PATCH /v1/publications/comments/3fa85f64-5717-4562-b3fc-2c963f66afa6
    ///     {
    ///       "text": "Updated comment text"
    ///     }
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <param name="comment">Updated comment data</param>
    /// <response code="200">Updated comment</response>
    /// <response code="400">Invalid comment data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot edit this comment</response>
    /// <response code="404">Comment not found</response>
    [HttpPatch("comments/{id}", Name = nameof(PatchPublicationComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchPublicationComment(Guid id, [FromBody] Comment comment) =>
        Ok(await _commentApiService.Update(id, comment));

    /// <summary>
    /// Delete publication comment
    /// </summary>
    /// <remarks>
    /// Permanently deletes a comment.
    /// Only the comment author or moderators can delete comments.
    /// This action cannot be undone.
    ///
    /// Example request:
    ///     DELETE /v1/publications/comments/3fa85f64-5717-4562-b3fc-2c963f66afa6
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="204">Comment deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot delete this comment</response>
    /// <response code="404">Comment not found</response>
    [HttpDelete("comments/{id}", Name = nameof(DeletePublicationComment))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePublicationComment(Guid id)
    {
        await _commentApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Like a publication comment
    /// </summary>
    /// <remarks>
    /// Adds a like to the comment from the current user.
    /// Users cannot like their own comments.
    /// Each user can only like a comment once.
    ///
    /// Example request:
    ///     POST /v1/publications/comments/3fa85f64-5717-4562-b3fc-2c963f66afa6/likes
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="201">Like added, returns user who liked</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot like this comment (e.g., own comment)</response>
    /// <response code="404">Comment not found</response>
    /// <response code="409">User already liked this comment</response>
    [HttpPost("comments/{id}/likes", Name = nameof(PostPublicationCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostPublicationCommentLike(Guid id)
    {
        var result = await _likeApiService.LikePublicationComment(id);
        return CreatedAtRoute(nameof(GetPublicationComment), new { id }, result);
    }

    /// <summary>
    /// Remove like from publication comment
    /// </summary>
    /// <remarks>
    /// Removes the current user's like from the comment.
    /// Can only remove your own likes.
    ///
    /// Example request:
    ///     DELETE /v1/publications/comments/3fa85f64-5717-4562-b3fc-2c963f66afa6/likes
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="204">Like removed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot remove like (not their like)</response>
    /// <response code="404">Comment not found</response>
    /// <response code="409">User has not liked this comment</response>
    [HttpDelete("comments/{id}/likes", Name = nameof(DeletePublicationCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePublicationCommentLike(Guid id)
    {
        await _likeApiService.UnlikePublicationComment(id);
        return NoContent();
    }
}
