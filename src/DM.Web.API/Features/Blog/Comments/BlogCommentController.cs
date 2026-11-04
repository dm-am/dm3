using System;
using DM.Domain.Core.Comments;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Blog.Likes;
using DM.Web.API.Features.Blog.Blogs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CommentRequest = DM.Web.API.Shared.Dto.CommentRequest;

namespace DM.Web.API.Features.Blog.Comments;

/// <summary>
/// Blog discussion comment management endpoints
/// </summary>
[ApiController]
[Route("v1/blogs")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Comments")]
public class BlogCommentController : ControllerBase
{
    private readonly BlogCommentApiService _commentApiService;
    private readonly BlogLikeApiService _likeApiService;
    private readonly BlogApiService _blogApiService;

    public BlogCommentController(
        BlogCommentApiService commentApiService,
        BlogLikeApiService likeApiService,
        BlogApiService blogApiService)
    {
        _commentApiService = commentApiService;
        _likeApiService = likeApiService;
        _blogApiService = blogApiService;
    }

    /// <summary>
    /// Get list of comments on blog
    /// </summary>
    /// <remarks>
    /// Returns paginated list of comments on the blog itself (Blog discussion).
    /// Supports filtering by author usernames, text search, date range and sorting.
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="q">Query parameters with filtering, sorting and pagination</param>
    /// <response code="200">Paginated list of comments</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("{id}/comments", Name = nameof(GetBlogComments))]
    [ProducesResponseType(typeof(ListEnvelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogComments(string id, [FromQuery] CommentsQuery q)
    {
        var blogId = await _blogApiService.ResolveId(id);
        return Ok(await _commentApiService.Get(blogId, q));
    }

    /// <summary>
    /// Create new comment on blog
    /// </summary>
    /// <remarks>
    /// Adds a new comment to the blog discussion.
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="request">Comment creation request with text content</param>
    /// <response code="201">Comment created successfully</response>
    /// <response code="400">Invalid comment data (empty text, etc.)</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot comment (blacklisted, banned, or no permission)</response>
    /// <response code="404">Blog not found</response>
    [HttpPost("{id}/comments", Name = nameof(PostBlogComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostBlogComment(string id, [FromBody] CommentRequest request)
    {
        var blogId = await _blogApiService.ResolveId(id);
        var result = await _commentApiService.Create(blogId, request);
        return CreatedAtRoute(nameof(GetBlogComment), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get blog comment
    /// </summary>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="200">Comment details</response>
    /// <response code="404">Comment not found, or it is not a comment of a blog the user may open</response>
    [HttpGet("comments/{id}", Name = nameof(GetBlogComment))]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogComment(Guid id) => Ok(await _commentApiService.Get(id));

    /// <summary>
    /// Update blog comment
    /// </summary>
    /// <remarks>
    /// Updates the comment text.
    /// Only the comment author or moderators can edit comments.
    /// Edit history is preserved.
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <param name="request">Updated comment text</param>
    /// <response code="200">Updated comment</response>
    /// <response code="400">Invalid comment data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot edit this comment</response>
    /// <response code="404">Comment not found</response>
    [HttpPatch("comments/{id}", Name = nameof(PatchBlogComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchBlogComment(Guid id, [FromBody] CommentRequest request) =>
        Ok(await _commentApiService.Update(id, request));

    /// <summary>
    /// Delete blog comment
    /// </summary>
    /// <remarks>
    /// Permanently deletes a blog comment.
    /// Only the comment author or moderators can delete comments.
    /// This action cannot be undone.
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="204">Comment deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot delete this comment</response>
    /// <response code="404">Comment not found</response>
    [HttpDelete("comments/{id}", Name = nameof(DeleteBlogComment))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="200">Like added, returns user who liked</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot like this comment (e.g., own comment)</response>
    /// <response code="404">Comment not found</response>
    /// <response code="409">User already liked this comment</response>
    [HttpPost("comments/{id}/likes", Name = nameof(PostBlogCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostBlogCommentLike(Guid id) =>
        Ok(await _likeApiService.LikeBlogComment(id));

    /// <summary>
    /// Remove like from blog comment
    /// </summary>
    /// <remarks>
    /// Removes the current user's like from the blog comment.
    /// Can only remove your own likes.
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <response code="204">All comments marked as read</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Blog not found</response>
    [HttpDelete("{id}/comments/unread", Name = nameof(MarkBlogCommentsAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkBlogCommentsAsRead(string id)
    {
        var blogId = await _blogApiService.ResolveId(id);
        await _commentApiService.MarkAsRead(blogId);
        return NoContent();
    }
}
