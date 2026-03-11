using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.UserReviews;
using DM.Domain.Core.Users;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Reviews;

/// <summary>
/// User reviews collection - list and create
/// </summary>
[ApiController]
[Route("v1/users/{username}/reviews")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Users")]
public class UserReviewController : ControllerBase
{
    private readonly IUserReviewService _userReviewService;
    private readonly IUserLookupService _userLookupService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserReviewController(
        IUserReviewService userReviewService,
        IUserLookupService userLookupService,
        IMapper mapper)
    {
        _userReviewService = userReviewService;
        _userLookupService = userLookupService;
        _mapper = mapper;
    }

    /// <summary>
    /// Get reviews for a user
    /// </summary>
    /// <remarks>
    /// Returns reviews written about the specified user.
    /// Users can only review other users they have played with in the same game.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">List of user reviews</response>
    /// <response code="404">User not found</response>
    [HttpGet(Name = nameof(GetUserReviews))]
    [ProducesResponseType(typeof(ListEnvelope<Review>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserReviews(string username, [FromQuery] PagingQuery q)
    {
        var user = await _userLookupService.GetAsync(username);
        var (reviews, paging) = await _userReviewService.GetListAsync(user.UserId, q);
        var apiReviews = reviews.Select(_mapper.Map<Review>);
        return Ok(new ListEnvelope<Review>(apiReviews, new PagingInfo(paging)));
    }

    /// <summary>
    /// Create user review
    /// </summary>
    /// <remarks>
    /// Creates a review for the specified user.
    /// You can only review users you have played with in the same game.
    /// Only one review per user is allowed.
    /// </remarks>
    /// <param name="username">Username of user to review</param>
    /// <param name="request">Review data</param>
    /// <response code="201">Review created successfully</response>
    /// <response code="400">Invalid review data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (haven't played together or reviewing yourself)</response>
    /// <response code="404">User not found</response>
    /// <response code="409">Review already exists</response>
    [HttpPost(Name = nameof(CreateUserReview))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Review), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUserReview(string username, [FromBody] CreateReviewRequest request)
    {
        var user = await _userLookupService.GetAsync(username);
        var createReview = new CreateUserReview
        {
            TargetUserId = user.UserId,
            Text = request.Text ?? string.Empty
        };
        var review = await _userReviewService.CreateAsync(createReview);
        var apiReview = _mapper.Map<Review>(review);
        return CreatedAtRoute(nameof(ReviewController.GetReview), new { id = review.Id }, apiReview);
    }
}
