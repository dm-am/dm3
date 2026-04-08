using System;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Comments;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Blog.Likes;
using DM.Web.API.Features.Blog.Blogs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Blog.Comments;

/// <summary>
/// Blog discussion comment management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for comments on the blog itself (Blog discussion).
/// These are comments directly on the blog, not on individual publications.
///
/// ## Available Operations
/// - List comments on a blog (with pagination)
/// - Create, read, update, delete comments
/// - Like/unlike comments
/// - Mark all blog comments as read
///
/// ## Access Control
/// - Viewing: Depends on blog ViewPolicy
/// - Creating: Authenticated users (if blog has CommentsEnabled)
/// - Editing: Author or moderators
/// - Deleting: Author or moderators
/// - Liking: Authenticated users (cannot like own comments)
/// </remarks>
[ApiController]
[Route("v1/blogs")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Comments")]
public class BlogCommentController : ControllerBase
{
    private readonly IBlogCommentApiService _commentApiService;
    private readonly IBlogLikeApiService _likeApiService;
    private readonly IBlogApiService _blogApiService;

    /// <summary>
    /// Creates a new instance of BlogCommentController
    /// </summary>
    public BlogCommentController(
        IBlogCommentApiService commentApiService,
        IBlogLikeApiService likeApiService,
        IBlogApiService blogApiService)
    {
        _commentApiService = commentApiService;
        _likeApiService = likeApiService;
        _blogApiService = blogApiService;
    }

    private async Task<Guid> ResolveBlogId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : (await _blogApiService.GetByPublicId(id)).Resource.Id;

    /// <summary>
    /// Get list of comments on blog
    /// </summary>
    /// <remarks>
    /// Returns paginated list of comments on the blog itself (Blog discussion).
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
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="q">Query parameters with filtering, sorting and pagination</param>
    /// <response code="200">Paginated list of comments</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("{id}/comments", Name = nameof(GetBlogComments))]
    [ProducesResponseType(typeof(ListEnvelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogComments(string id, [FromQuery] BlogCommentsQuery q)
    {
        var blogId = await ResolveBlogId(id);
        var (comments, paging) = await _commentApiService.Get(blogId, q);
        return Ok(new ListEnvelope<Comment>(comments, paging));
    }

    /// <summary>
    /// Create new comment on blog
    /// </summary>
    /// <remarks>
    /// Adds a new comment to the blog discussion.
    /// Requires blog to have CommentsEnabled.
    ///
    /// Example request:
    ///     POST /v1/blogs/abcde/comments
    ///     {
    ///       "text": "Welcome to the blog!"
    ///     }
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="request">Comment creation request with text content</param>
    /// <response code="201">Comment created successfully</response>
    /// <response code="400">Invalid comment data (empty text, etc.)</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot comment (comments disabled or no permission)</response>
    /// <response code="404">Blog not found</response>
    [HttpPost("{id}/comments", Name = nameof(PostBlogComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Comment), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostBlogComment(string id, [FromBody] CreateCommentRequest request)
    {
        var blogId = await ResolveBlogId(id);
        var result = await _commentApiService.Create(blogId, request);
        return CreatedAtRoute(nameof(GetBlogComment), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get blog comment
    /// </summary>
    /// <remarks>
    /// Returns detailed information about a specific blog comment including:
    /// - Comment text and author
    /// - Creation and last edit timestamps
    /// - Like count
    ///
    /// Example request:
    ///     GET /v1/blogs/comments/3fa85f64-5717-4562-b3fc-2c963f66afa6
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="200">Comment details</response>
    /// <response code="404">Comment not found</response>
    [HttpGet("comments/{id}", Name = nameof(GetBlogComment))]
    [ProducesResponseType(typeof(Comment), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogComment(Guid id) => Ok(await _commentApiService.Get(id));

    /// <summary>
    /// Update blog comment
    /// </summary>
    /// <remarks>
    /// Updates the comment text.
    /// Only the comment author or moderators can edit comments.
    /// Edit history is preserved.
    ///
    /// Example request:
    ///     PATCH /v1/blogs/comments/3fa85f64-5717-4562-b3fc-2c963f66afa6
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
    [HttpPatch("comments/{id}", Name = nameof(PatchBlogComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Comment), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchBlogComment(Guid id, [FromBody] Comment comment) =>
        Ok(await _commentApiService.Update(id, comment));

    /// <summary>
    /// Delete blog comment
    /// </summary>
    /// <remarks>
    /// Permanently deletes a blog comment.
    /// Only the comment author or moderators can delete comments.
    /// This action cannot be undone.
    ///
    /// Example request:
    ///     DELETE /v1/blogs/comments/3fa85f64-5717-4562-b3fc-2c963f66afa6
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="204">Comment deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot delete this comment</response>
    /// <response code="404">Comment not found</response>
    [HttpDelete("comments/{id}", Name = nameof(DeleteBlogComment))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBlogComment(Guid id)
    {
        await _commentApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Like a blog comment
    /// </summary>
    /// <remarks>
    /// Adds a like to the blog comment from the current user.
    /// Users cannot like their own comments.
    /// Each user can only like a comment once.
    ///
    /// Example request:
    ///     POST /v1/blogs/comments/3fa85f64-5717-4562-b3fc-2c963f66afa6/likes
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="201">Like added, returns user who liked</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot like this comment (e.g., own comment)</response>
    /// <response code="404">Comment not found</response>
    /// <response code="409">User already liked this comment</response>
    [HttpPost("comments/{id}/likes", Name = nameof(PostBlogCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostBlogCommentLike(Guid id)
    {
        var result = await _likeApiService.LikeBlogComment(id);
        return CreatedAtRoute(nameof(GetBlogComment), new { id }, result);
    }

    /// <summary>
    /// Remove like from blog comment
    /// </summary>
    /// <remarks>
    /// Removes the current user's like from the blog comment.
    /// Can only remove your own likes.
    ///
    /// Example request:
    ///     DELETE /v1/blogs/comments/3fa85f64-5717-4562-b3fc-2c963f66afa6/likes
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="204">Like removed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot remove like (not their like)</response>
    /// <response code="404">Comment not found</response>
    /// <response code="409">User has not liked this comment</response>
    [HttpDelete("comments/{id}/likes", Name = nameof(DeleteBlogCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteBlogCommentLike(Guid id)
    {
        await _likeApiService.UnlikeBlogComment(id);
        return NoContent();
    }

    /// <summary>
    /// Mark all blog comments as read
    /// </summary>
    /// <remarks>
    /// Marks all comments on the blog as read for the current user.
    /// Useful for clearing unread counters.
    ///
    /// Example request:
    ///     DELETE /v1/blogs/abcde/comments/unread
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <response code="204">All comments marked as read</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Blog not found</response>
    [HttpDelete("{id}/comments/unread", Name = nameof(MarkBlogCommentsAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkBlogCommentsAsRead(string id)
    {
        var blogId = await ResolveBlogId(id);
        await _commentApiService.MarkAsRead(blogId);
        return NoContent();
    }
}
