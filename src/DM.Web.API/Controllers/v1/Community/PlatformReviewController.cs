using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.BusinessProcesses.Reviews.Creating;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
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
/// Platform reviews API - reviews about the DM platform itself
/// </summary>
[ApiController]
[Route("v1/reviews/platform")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Reviews")]
public class PlatformReviewController : ControllerBase
{
    private readonly IReviewReadingService _readingService;
    private readonly IReviewCreatingService _creatingService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IMapper _mapper;

    /// <summary>
    /// Creates a new instance of PlatformReviewController
    /// </summary>
    public PlatformReviewController(
        IReviewReadingService readingService,
        IReviewCreatingService creatingService,
        IIdentityProvider identityProvider,
        IMapper mapper)
    {
        _readingService = readingService;
        _creatingService = creatingService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    /// <summary>
    /// Get platform reviews
    /// </summary>
    /// <remarks>
    /// Returns list of platform/site reviews.
    /// By default returns only approved reviews.
    /// Admins (SeniorModerator+) can use `approved=false` to see all reviews.
    /// </remarks>
    /// <param name="q">Paging parameters</param>
    /// <param name="approved">Filter by approval status. null=approved only (default), false=all (admin only)</param>
    /// <response code="200">List of platform reviews</response>
    [HttpGet(Name = nameof(GetPlatformReviews))]
    [ProducesResponseType(typeof(ListEnvelope<ApiReview>), 200)]
    public async Task<IActionResult> GetPlatformReviews(
        [FromQuery] PagingQuery q,
        [FromQuery] bool? approved = null)
    {
        // If approved=false is requested, check if user has admin privileges
        var onlyApproved = true;
        if (approved == false)
        {
            var currentUser = _identityProvider.Current.User;
            if (currentUser.Role >= UserRole.SeniorModerator)
            {
                onlyApproved = false;
            }
            // If user doesn't have admin role, silently fallback to approved-only
        }

        var (reviews, paging) = await _readingService.Get(q, onlyApproved: onlyApproved);
        var apiReviews = reviews.Select(_mapper.Map<ApiReview>);
        return Ok(new ListEnvelope<ApiReview>(apiReviews, new Paging(paging)));
    }

    /// <summary>
    /// Create platform review
    /// </summary>
    /// <remarks>
    /// Creates a new platform/site review. Reviews may require moderation before being visible.
    /// </remarks>
    /// <param name="request">Review data</param>
    /// <response code="201">Review created successfully</response>
    /// <response code="400">Invalid review data</response>
    /// <response code="401">User not authenticated</response>
    [HttpPost(Name = nameof(CreatePlatformReview))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ApiReview>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> CreatePlatformReview([FromBody] CreateReviewRequest request)
    {
        var createReview = new CreateReview
        {
            Text = request.Text ?? string.Empty
        };
        var review = await _creatingService.Create(createReview);
        var apiReview = _mapper.Map<ApiReview>(review);
        return CreatedAtRoute(nameof(ReviewController.GetReview), new { id = review.Id }, new Envelope<ApiReview>(apiReview));
    }
}
