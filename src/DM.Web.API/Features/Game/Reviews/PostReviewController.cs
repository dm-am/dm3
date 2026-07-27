using DM.Web.API.Shared.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.PostReviews;
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
    private readonly IPostReviewService _ratedPostReviewService;
    private readonly IUserLookupService _userLookupService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostReviewController(
        IPostReviewService ratedPostReviewService,
        IUserLookupService userLookupService,
        IMapper mapper)
    {
        _ratedPostReviewService = ratedPostReviewService;
        _userLookupService = userLookupService;
        _mapper = mapper;
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
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllPostReviews(
        [FromQuery] PagingQuery q,
        [FromQuery] string? authorUsername = null,
        [FromQuery] string? recipientUsername = null,
        [FromQuery] Guid? gameId = null)
    {
        var filter = new PostReviewFilter
        {
            GameId = gameId
        };

        if (!string.IsNullOrWhiteSpace(authorUsername))
        {
            var author = await _userLookupService.GetAsync(authorUsername);
            filter.AuthorId = author.UserId;
        }

        if (!string.IsNullOrWhiteSpace(recipientUsername))
        {
            var recipient = await _userLookupService.GetAsync(recipientUsername);
            filter.RecipientId = recipient.UserId;
        }

        var (reviews, paging) = await _ratedPostReviewService.GetAllAsync(q, filter);
        var apiReviews = reviews.Select(_mapper.Map<PostReviewDto>);
        return Ok(new ListEnvelope<PostReviewDto>(apiReviews, new PagingInfo(paging)));
    }

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
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostReviews(Guid postId, [FromQuery] PagingQuery q)
    {
        var (reviews, paging) = await _ratedPostReviewService.GetListAsync(postId, q);
        var apiReviews = reviews.Select(_mapper.Map<PostReviewDto>);
        return Ok(new ListEnvelope<PostReviewDto>(apiReviews, new PagingInfo(paging)));
    }

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
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreatePostReview(Guid postId, [FromBody] CreatePostReviewRequest request)
    {
        var createReview = new CreatePostReview
        {
            PostId = postId,
            Sign = request.Sign,
            Text = request.Text
        };
        var review = await _ratedPostReviewService.CreateAsync(createReview);
        var apiReview = _mapper.Map<PostReviewDto>(review);
        return CreatedAtRoute(nameof(GetPostReview), new { postId, reviewId = review.Id }, new Envelope<PostReviewDto>(apiReview));
    }

    /// <summary>
    /// Get single post review by ID
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="reviewId">Review ID</param>
    /// <response code="200">post review</response>
    /// <response code="404">Review not found</response>
    [HttpGet("~/v1/posts/{postId:guid}/reviews/{reviewId:guid}", Name = nameof(GetPostReview))]
    [ProducesResponseType(typeof(Envelope<PostReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostReview(Guid postId, Guid reviewId)
    {
        var review = await _ratedPostReviewService.GetAsync(reviewId);
        return Ok(new Envelope<PostReviewDto>(_mapper.Map<PostReviewDto>(review)));
    }
}
