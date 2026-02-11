using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Reviews.Creating;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ApiReview = DM.Web.API.Dto.Community.Review;
using CreateReviewRequest = DM.Web.API.Dto.Community.CreateReviewRequest;

namespace DM.Web.API.Controllers.v1.Community;

/// <summary>
/// User reviews collection - list and create
/// </summary>
[ApiController]
[Route("v1/users/{login}/reviews")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Reviews")]
public class UserReviewCollectionController : ControllerBase
{
    private readonly IReviewReadingService _readingService;
    private readonly IReviewCreatingService _creatingService;
    private readonly IUserReadingService _userReadingService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserReviewCollectionController(
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
    /// Get reviews for a user
    /// </summary>
    /// <remarks>
    /// Returns reviews written about the specified user.
    /// Users can only review other users they have played with in the same game.
    /// </remarks>
    /// <param name="login">User login</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">List of user reviews</response>
    /// <response code="404">User not found</response>
    [HttpGet(Name = nameof(GetUserReviews))]
    [ProducesResponseType(typeof(ListEnvelope<ApiReview>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetUserReviews(string login, [FromQuery] PagingQuery q)
    {
        var user = await _userReadingService.Get(login);
        var (reviews, paging) = await _readingService.GetUserReviews(user.UserId, q);
        var apiReviews = reviews.Select(_mapper.Map<ApiReview>);
        return Ok(new ListEnvelope<ApiReview>(apiReviews, new Paging(paging)));
    }

    /// <summary>
    /// Create user review
    /// </summary>
    /// <remarks>
    /// Creates a review for the specified user.
    /// You can only review users you have played with in the same game.
    /// Only one review per user is allowed.
    /// </remarks>
    /// <param name="login">User login to review</param>
    /// <param name="request">Review data</param>
    /// <response code="201">Review created successfully</response>
    /// <response code="400">Invalid review data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (haven't played together or reviewing yourself)</response>
    /// <response code="404">User not found</response>
    /// <response code="409">Review already exists</response>
    [HttpPost(Name = nameof(CreateUserReview))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ApiReview>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    public async Task<IActionResult> CreateUserReview(string login, [FromBody] CreateReviewRequest request)
    {
        var user = await _userReadingService.Get(login);
        var review = await _creatingService.CreateUserReview(user.UserId, request.Text ?? string.Empty);
        var apiReview = _mapper.Map<ApiReview>(review);
        return CreatedAtRoute(nameof(ReviewController.GetReview), new { id = review.Id }, new Envelope<ApiReview>(apiReview));
    }
}
