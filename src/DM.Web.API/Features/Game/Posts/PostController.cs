using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Posts;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// Post management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for game posts (in-character messages).
/// Posts belong to rooms and can be written by characters.
/// </remarks>
[ApiController]
[Route("v1/posts")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Posts")]
public class PostController : ControllerBase
{
    private readonly IPostApiService _postApiService;

    /// <summary>
    /// Creates a new instance of PostController
    /// </summary>
    public PostController(IPostApiService postApiService)
    {
        _postApiService = postApiService;
    }

    /// <summary>
    /// Get list of posts in room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">Returns the list of posts in the room</response>
    /// <response code="404">Room not found</response>
    [HttpGet("~/v1/rooms/{id}/posts", Name = nameof(GetPosts))]
    [ProducesResponseType(typeof(ListEnvelope<Post>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosts(Guid id, [FromQuery] PagingQuery q) =>
        Ok(await _postApiService.Get(id, q));

    /// <summary>
    /// Create new post in room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <param name="post">Post details</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of post parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create post in this room</response>
    /// <response code="404">Room not found</response>
    [HttpPost("~/v1/rooms/{id}/posts", Name = nameof(PostPost))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Post>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostPost(Guid id, [FromBody] CreatePostRequest post)
    {
        var result = await _postApiService.Create(id, post);
        return CreatedAtRoute(nameof(GetPost),
            new {id = result.Resource.Id}, result);
    }

    /// <summary>
    /// Get post
    /// </summary>
    /// <param name="id">Post identifier</param>
    /// <response code="200">Returns the post details</response>
    /// <response code="404">Post not found</response>
    [HttpGet("{id}", Name = nameof(GetPost))]
    [ProducesResponseType(typeof(Envelope<Post>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPost(Guid id) => Ok(await _postApiService.Get(id));

    /// <summary>
    /// Update post
    /// </summary>
    /// <param name="id">Post identifier</param>
    /// <param name="post">Updated post details</param>
    /// <response code="200">Returns the updated post</response>
    /// <response code="400">Some of post changed properties were invalid or passed id was not recognized</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change some properties of this post</response>
    /// <response code="404">Post not found</response>
    [HttpPatch("{id}", Name = nameof(PatchPost))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Post>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchPost(Guid id, [FromBody] Post post) =>
        Ok(await _postApiService.Update(id, post));

    /// <summary>
    /// Delete post
    /// </summary>
    /// <param name="id">Post identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove the post</response>
    /// <response code="404">Post not found</response>
    [HttpDelete("{id}", Name = nameof(DeletePost))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        await _postApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Get posts with rating info
    /// </summary>
    /// <remarks>
    /// Returns posts sorted by rating or review date.
    ///
    /// Sort options (sortBy):
    /// - rating: Sort by sum of reviews (default)
    /// - lastreview: Sort by most recent review
    /// - reviewcount: Sort by number of reviews
    /// - created: Sort by post creation date
    ///
    /// Filters:
    /// - hasReviews: Only posts with at least one review
    /// - lastReviewedAfter: Posts with last review after this date (ISO 8601)
    /// - gameId: Filter by specific game
    /// - minRating / maxRating: Rating range (can be negative)
    /// - authorUsernames: Comma-separated post author usernames
    /// - createdAfter / createdBefore: Post creation date range (ISO 8601)
    /// </remarks>
    /// <param name="query">Filter and sorting parameters</param>
    /// <response code="200">Returns the list of rated posts</response>
    [HttpGet(Name = nameof(GetRatedPosts))]
    [ProducesResponseType(typeof(ListEnvelope<Post>), StatusCodes.Status200OK)]
    // Client-only cache: the response is personalized (unread counters, private rooms),
    // so it must never be stored by shared caches or the server response cache
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetRatedPosts([FromQuery] PostsQuery query) =>
        Ok(await _postApiService.GetRated(query));
}
