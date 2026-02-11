using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Reviews.Creating;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ApiReview = DM.Web.API.Dto.Community.Review;
using CreateReviewRequest = DM.Web.API.Dto.Community.CreateReviewRequest;

namespace DM.Web.API.Controllers.v1.Community;

/// <summary>
/// Post reviews API - list and create reviews on game posts
/// </summary>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Reviews")]
public class PostReviewCollectionController : ControllerBase
{
    private readonly IReviewReadingService _readingService;
    private readonly IReviewCreatingService _creatingService;
    private readonly IUserReadingService _userReadingService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostReviewCollectionController(
        IReviewReadingService readingService,
        IReviewCreatingService creatingService,
        IUserReadingService userReadingService,
        IMapper mapper)
    {
        _readingService = readingService;
        _creatingService = creatingService;
        _userReadingService = userReadingService;
        _mapper = mapper;
    }

    /// <summary>
    /// Get all post reviews with optional filters
    /// </summary>
    /// <remarks>
    /// Returns post reviews (ratings) across all games with optional filtering.
    ///
    /// Filter options:
    /// - **authorLogin**: Reviews written BY this user
    /// - **recipientLogin**: Reviews ON posts of this user
    /// - **gameId**: Reviews on posts in specific game
    /// </remarks>
    /// <param name="q">Paging parameters</param>
    /// <param name="authorLogin">Filter by review author</param>
    /// <param name="recipientLogin">Filter by post author (recipient)</param>
    /// <param name="gameId">Filter by game</param>
    /// <response code="200">List of post reviews</response>
    [HttpGet("reviews/posts", Name = nameof(GetAllPostReviews))]
    [ProducesResponseType(typeof(ListEnvelope<ApiReview>), 200)]
    public async Task<IActionResult> GetAllPostReviews(
        [FromQuery] PagingQuery q,
        [FromQuery] string? authorLogin = null,
        [FromQuery] string? recipientLogin = null,
        [FromQuery] Guid? gameId = null)
    {
        var filter = new PostReviewFilter
        {
            GameId = gameId
        };

        if (!string.IsNullOrWhiteSpace(authorLogin))
        {
            var author = await _userReadingService.Get(authorLogin);
            filter.AuthorId = author.UserId;
        }

        if (!string.IsNullOrWhiteSpace(recipientLogin))
        {
            var recipient = await _userReadingService.Get(recipientLogin);
            filter.RecipientId = recipient.UserId;
        }

        var (reviews, paging) = await _readingService.GetAllPostReviews(q, filter);
        var apiReviews = reviews.Select(_mapper.Map<ApiReview>);
        return Ok(new ListEnvelope<ApiReview>(apiReviews, new Paging(paging)));
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
    [HttpGet("posts/{postId:guid}/reviews", Name = nameof(GetPostReviews))]
    [ProducesResponseType(typeof(ListEnvelope<ApiReview>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetPostReviews(Guid postId, [FromQuery] PagingQuery q)
    {
        var (reviews, paging) = await _readingService.GetPostReviews(postId, q);
        var apiReviews = reviews.Select(_mapper.Map<ApiReview>);
        return Ok(new ListEnvelope<ApiReview>(apiReviews, new Paging(paging)));
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
    [HttpPost("posts/{postId:guid}/reviews", Name = nameof(CreatePostReview))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ApiReview>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    public async Task<IActionResult> CreatePostReview(Guid postId, [FromBody] CreateReviewRequest request)
    {
        if (!request.Sign.HasValue)
        {
            return BadRequest(new BadRequestError("Sign is required for post reviews",
                new Dictionary<string, IEnumerable<string>>
                {
                    ["Sign"] = ["Sign is required for post reviews"]
                }));
        }

        var review = await _creatingService.CreatePostReview(postId, request.Sign.Value, request.ReasonType);
        var apiReview = _mapper.Map<ApiReview>(review);
        return CreatedAtRoute(nameof(ReviewController.GetReview), new { id = review.Id }, new Envelope<ApiReview>(apiReview));
    }
}
