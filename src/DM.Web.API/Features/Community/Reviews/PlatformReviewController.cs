using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Community.Features.PlatformReviews;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Reviews;

/// <summary>
/// Platform reviews API - reviews about the DM platform itself
/// </summary>
[ApiController]
[Route("v1/reviews/platform")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Reviews")]
public class PlatformReviewController : ControllerBase
{
    private readonly IPlatformReviewService _platformReviewService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IMapper _mapper;

    /// <summary>
    /// Creates a new instance of PlatformReviewController
    /// </summary>
    public PlatformReviewController(
        IPlatformReviewService platformReviewService,
        IIdentityProvider identityProvider,
        IMapper mapper)
    {
        _platformReviewService = platformReviewService;
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
    [ProducesResponseType(typeof(ListEnvelope<Review>), 200)]
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

        var (reviews, paging) = await _platformReviewService.GetListAsync(q, onlyApproved: onlyApproved);
        var apiReviews = reviews.Select(_mapper.Map<Review>);
        return Ok(new ListEnvelope<Review>(apiReviews, new PagingInfo(paging)));
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
    [ProducesResponseType(typeof(Envelope<Review>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> CreatePlatformReview([FromBody] CreateReviewRequest request)
    {
        var createReview = new CreatePlatformReview
        {
            Text = request.Text ?? string.Empty
        };
        var review = await _platformReviewService.CreateAsync(createReview);
        var apiReview = _mapper.Map<Review>(review);
        return CreatedAtRoute(nameof(ReviewController.GetReview), new { id = review.Id }, new Envelope<Review>(apiReview));
    }
}
