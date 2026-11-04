using System;
using DM.Domain.Core.Comments;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Blog.Likes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CommentRequest = DM.Web.API.Shared.Dto.CommentRequest;

namespace DM.Web.API.Features.Blog.PublicationComments;

/// <summary>
/// Publication comment management endpoints
/// </summary>
[ApiController]
[Route("v1/publications")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Publication Comments")]
public class PublicationCommentController : ControllerBase
{
    private readonly PublicationCommentApiService _commentApiService;
    private readonly BlogLikeApiService _likeApiService;

    public PublicationCommentController(
        PublicationCommentApiService commentApiService,
        BlogLikeApiService likeApiService)
    {
        _commentApiService = commentApiService;
        _likeApiService = likeApiService;
    }

    /// <summary>
    /// Get list of comments in publication
    /// </summary>
    /// <remarks>
    /// Returns paginated list of comments in the specified publication.
    /// Supports filtering by author usernames, text search, date range and sorting.
    /// </remarks>
    /// <param name="id">Publication identifier (GUID)</param>
    /// <param name="q">Query parameters with filtering, sorting and pagination</param>
    /// <response code="200">Paginated list of comments</response>
    /// <response code="404">Publication not found</response>
    [HttpGet("{id}/comments", Name = nameof(GetPublicationComments))]
    [ProducesResponseType(typeof(ListEnvelope<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicationComments(Guid id, [FromQuery] CommentsQuery q) =>
        Ok(await _commentApiService.Get(id, q));

    /// <summary>
    /// Create new comment in publication
    /// </summary>
    /// <remarks>
    /// Adds a new comment to the specified publication.
    /// Cannot add comments to closed publications.
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
    public async Task<IActionResult> PostPublicationComment(Guid id, [FromBody] CommentRequest request)
    {
        var result = await _commentApiService.Create(id, request);
        return CreatedAtRoute(nameof(GetPublicationComment), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get publication comment
    /// </summary>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="200">Comment details</response>
    /// <response code="404">Comment not found, or it is not a comment of a publication the user may open</response>
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
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <param name="request">Updated comment text</param>
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
    public async Task<IActionResult> PatchPublicationComment(Guid id, [FromBody] CommentRequest request) =>
        Ok(await _commentApiService.Update(id, request));

    /// <summary>
    /// Delete publication comment
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
    /// </remarks>
    /// <param name="id">Comment identifier (GUID)</param>
    /// <response code="200">Like added, returns user who liked</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot like this comment (e.g., own comment)</response>
    /// <response code="404">Comment not found</response>
    /// <response code="409">User already liked this comment</response>
    [HttpPost("comments/{id}/likes", Name = nameof(PostPublicationCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostPublicationCommentLike(Guid id) =>
        Ok(await _likeApiService.LikePublicationComment(id));

    /// <summary>
    /// Remove like from publication comment
    /// </summary>
    /// <remarks>
    /// Removes the current user's like from the comment.
    /// Can only remove your own likes.
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
