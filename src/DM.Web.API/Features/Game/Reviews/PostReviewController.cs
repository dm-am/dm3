using DM.Web.API.Shared.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.Reviews;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ApiReview = DM.Web.API.Features.Community.Reviews.Review;
using CreateReviewRequest = DM.Web.API.Features.Community.Reviews.CreateReviewRequest;
using ReviewController = DM.Web.API.Features.Community.Reviews.ReviewController;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// Post reviews API - list and create reviews on game posts
/// </summary>
[ApiController]
[Route("v1/reviews")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Posts")]
public class PostReviewController : ControllerBase
{
    private readonly IPostReviewService _postReviewService;
    private readonly IUserLookupService _userLookupService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostReviewController(
        IPostReviewService postReviewService,
        IUserLookupService userLookupService,
        IMapper mapper)
    {
        _postReviewService = postReviewService;
        _userLookupService = userLookupService;
        _mapper = mapper;
    }

    /// <summary>
    /// Get all post reviews with optional filters
    /// </summary>
    /// <remarks>
    /// Returns post reviews (ratings) across all games with optional filtering.
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
    [HttpGet("posts", Name = nameof(GetAllPostReviews))]
    [ProducesResponseType(typeof(ListEnvelope<ApiReview>), 200)]
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
            var author = await _userLookupService.Get(authorUsername);
            filter.AuthorId = author.UserId;
        }

        if (!string.IsNullOrWhiteSpace(recipientUsername))
        {
            var recipient = await _userLookupService.Get(recipientUsername);
            filter.RecipientId = recipient.UserId;
        }

        var (reviews, paging) = await _postReviewService.GetAllAsync(q, filter);
        var apiReviews = reviews.Select(_mapper.Map<ApiReview>);
        return Ok(new ListEnvelope<ApiReview>(apiReviews, new PagingInfo(paging)));
    }

    /// <summary>
    /// Get reviews for a specific post
    /// </summary>
    /// <remarks>
    /// Returns all reviews (ratings) for the specified post.
    /// </remarks>
    /// <param name="postId">Post ID</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">List of post reviews</response>
    /// <response code="404">Post not found</response>
    [HttpGet("~/v1/posts/{postId:guid}/reviews", Name = nameof(GetPostReviews))]
    [ProducesResponseType(typeof(ListEnvelope<ApiReview>), 200)]
    [ProducesResponseType(typeof(ErrorEnvelope), 404)]
    public async Task<IActionResult> GetPostReviews(Guid postId, [FromQuery] PagingQuery q)
    {
        var (reviews, paging) = await _postReviewService.GetListAsync(postId, q);
        var apiReviews = reviews.Select(_mapper.Map<ApiReview>);
        return Ok(new ListEnvelope<ApiReview>(apiReviews, new PagingInfo(paging)));
    }

    /// <summary>
    /// Create post review
    /// </summary>
    /// <remarks>
    /// Creates a rating/review for the specified post.
    ///
    /// **Requirements:**
    /// - Cannot review your own posts
    /// - Cannot create multiple reviews for the same post
    /// - Must be authenticated
    /// - Newbie users (registered less than a week ago with no games) cannot create reviews
    ///
    /// **Request body:**
    /// - **sign** (required): Positive, Neutral, or Negative
    /// - **reasonType** (optional): Fun, Roleplay, Literature (flags)
    /// </remarks>
    /// <param name="postId">Post ID</param>
    /// <param name="request">Review data</param>
    /// <response code="201">Review created successfully</response>
    /// <response code="400">Invalid review data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (own post, newbie restriction)</response>
    /// <response code="404">Post not found</response>
    /// <response code="409">Review already exists for this post</response>
    [HttpPost("~/v1/posts/{postId:guid}/reviews", Name = nameof(CreatePostReview))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ApiReview), 201)]
    [ProducesResponseType(typeof(ErrorEnvelope), 400)]
    [ProducesResponseType(typeof(ErrorEnvelope), 401)]
    [ProducesResponseType(typeof(ErrorEnvelope), 403)]
    [ProducesResponseType(typeof(ErrorEnvelope), 404)]
    [ProducesResponseType(typeof(ErrorEnvelope), 409)]
    public async Task<IActionResult> CreatePostReview(Guid postId, [FromBody] CreateReviewRequest request)
    {
        if (!request.Sign.HasValue)
        {
            throw new HttpBadRequestException(
                new Dictionary<string, string> { ["Sign"] = "Sign is required for post reviews" },
                "Sign is required for post reviews");
        }

        var createReview = new CreatePostReview
        {
            PostId = postId,
            Sign = request.Sign.Value,
            ReasonType = request.ReasonType
        };
        var review = await _postReviewService.CreateAsync(createReview);
        var apiReview = _mapper.Map<ApiReview>(review);
        return CreatedAtRoute(nameof(ReviewController.GetReview), new { id = review.Id }, apiReview);
    }
}
