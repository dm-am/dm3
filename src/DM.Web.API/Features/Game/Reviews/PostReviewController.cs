using DM.Web.API.Shared.Authentication;
using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// post reviews API - list and create reviews on game posts
/// </summary>
[ApiController]
[Route("v1/reviews")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Posts")]
public class PostReviewController : ControllerBase
{
    private readonly IPostReviewApiService _postReviewApiService;

    /// <inheritdoc />
    public PostReviewController(IPostReviewApiService postReviewApiService)
    {
        _postReviewApiService = postReviewApiService;
    }

    /// <summary>
    /// Get all post reviews with optional filters (moderators only)
    /// </summary>
    /// <remarks>
    /// Returns post reviews across all games with optional filtering.
    /// Cross-game review listing is a moderation worklist ("Последние
    /// оцененные посты"); public pages consume per-post reviews
    /// (GET v1/posts/{postId}/reviews) or the rated posts list (GET v1/posts).
    ///
    /// Filter options:
    /// - **authorUsername**: Reviews written BY this user
    /// - **recipientUsername**: Reviews ON posts of this user
    /// - **gameId**: Reviews on posts in specific game
    /// </remarks>
    /// <param name="q">Paging parameters</param>
    /// <param name="authorUsername">Filter by review author</param>
    /// <param name="recipientUsername">Filter by post author (recipient)</param>
    /// <param name="gameId">Filter by game</param>
    /// <response code="200">List of post reviews</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Moderator or higher role required</response>
    [HttpGet("posts", Name = nameof(GetAllPostReviews))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<PostReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllPostReviews(
        [FromQuery] PagingQuery q,
        [FromQuery] string? authorUsername = null,
        [FromQuery] string? recipientUsername = null,
        [FromQuery] Guid? gameId = null) =>
        Ok(await _postReviewApiService.GetAll(q, authorUsername, recipientUsername, gameId));

    /// <summary>
    /// Get rated reviews for a specific post
    /// </summary>
    /// <remarks>
    /// Returns all rated reviews for the specified post.
    /// </remarks>
    /// <param name="postId">Post ID</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">List of post reviews</response>
    /// <response code="404">Post not found</response>
    [HttpGet("~/v1/posts/{postId:guid}/reviews", Name = nameof(GetPostReviews))]
    [ProducesResponseType(typeof(ListEnvelope<PostReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostReviews(Guid postId, [FromQuery] PagingQuery q) =>
        Ok(await _postReviewApiService.GetList(postId, q));

    /// <summary>
    /// Create post review
    /// </summary>
    /// <remarks>
    /// Creates a rated review for the specified post.
    ///
    /// **Requirements:**
    /// - Cannot review your own posts
    /// - Cannot create multiple reviews for the same post
    /// - Must be authenticated
    /// - Newbie users (less than 100 game posts) can only create neutral reviews
    /// - Cooldown: one review per game every 3 days
    ///
    /// **Request body:**
    /// - **sign** (required): Positive, Neutral, or Negative
    /// </remarks>
    /// <param name="postId">Post ID</param>
    /// <param name="request">Review data</param>
    /// <response code="201">Review created successfully</response>
    /// <response code="400">Invalid review data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (own post, newbie restriction)</response>
    /// <response code="404">Post not found</response>
    /// <response code="409">Review already exists for this post</response>
    /// <response code="429">Too many requests (cooldown period)</response>
    [HttpPost("~/v1/posts/{postId:guid}/reviews", Name = nameof(CreatePostReview))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<PostReviewDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreatePostReview(Guid postId, [FromBody] CreatePostReviewRequest request)
    {
        var result = await _postReviewApiService.Create(postId, request);
        return CreatedAtRoute(nameof(GetPostReview), new { postId, reviewId = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get single post review by ID
    /// </summary>
    /// <remarks>
    /// Read through the post: a review is only as readable as the post it is
    /// about, and a review belonging to another post answers as one that is not
    /// there.
    /// </remarks>
    /// <param name="postId">Post ID</param>
    /// <param name="reviewId">Review ID</param>
    /// <response code="200">post review</response>
    /// <response code="404">Post out of reach, or no such review of it</response>
    [HttpGet("~/v1/posts/{postId:guid}/reviews/{reviewId:guid}", Name = nameof(GetPostReview))]
    [ProducesResponseType(typeof(Envelope<PostReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostReview(Guid postId, Guid reviewId) =>
        Ok(await _postReviewApiService.Get(postId, reviewId));

    /// <summary>
    /// Update post review
    /// </summary>
    /// <remarks>
    /// Changes the sign, the text, or both; an omitted field keeps its stored value.
    ///
    /// **Requirements:**
    /// - Only the review's author may edit it. A moderator does not rewrite
    ///   somebody else's opinion — the moderation tool is DELETE below
    /// - The window is 15 minutes from publication, the same one a game post and
    ///   a comment give their author (admins are exempt)
    /// - The newbie restriction of the create path holds here too: below
    ///   100 game posts the sign may only stay neutral
    /// </remarks>
    /// <param name="postId">Post ID</param>
    /// <param name="reviewId">Review ID</param>
    /// <param name="request">Fields to change</param>
    /// <response code="200">Review updated</response>
    /// <response code="400">Invalid update data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (not the author, window expired, newbie restriction)</response>
    /// <response code="404">Review not found</response>
    [HttpPatch("~/v1/posts/{postId:guid}/reviews/{reviewId:guid}", Name = nameof(UpdatePostReview))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<PostReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePostReview(
        Guid postId, Guid reviewId, [FromBody] UpdatePostReviewRequest request) =>
        Ok(await _postReviewApiService.Update(reviewId, request));

    /// <summary>
    /// Delete post review
    /// </summary>
    /// <remarks>
    /// Soft-deletes the review and takes its sign back out of the post author's
    /// quality rating.
    ///
    /// **Requirements:**
    /// - The author, at any time — no window, as with a post and a comment
    /// - Or a senior moderator, on somebody else's review: taking down a stated
    ///   opinion sits a step above deleting a comment, and AUTHORIZATION.md puts
    ///   it with profile moderation
    /// </remarks>
    /// <param name="postId">Post ID</param>
    /// <param name="reviewId">Review ID</param>
    /// <response code="204">Review deleted</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (neither the author nor a senior moderator)</response>
    /// <response code="404">Review not found</response>
    [HttpDelete("~/v1/posts/{postId:guid}/reviews/{reviewId:guid}", Name = nameof(DeletePostReview))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePostReview(Guid postId, Guid reviewId)
    {
        await _postReviewApiService.Delete(reviewId);
        return NoContent();
    }
}
