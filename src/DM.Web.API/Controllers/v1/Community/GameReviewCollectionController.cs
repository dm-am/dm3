using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Reviews.Creating;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Core.Dto;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ApiReview = DM.Web.API.Dto.Community.Review;
using CreateReviewRequest = DM.Web.API.Dto.Community.CreateReviewRequest;

namespace DM.Web.API.Controllers.v1.Community;

/// <summary>
/// Game reviews collection - list and create
/// </summary>
[ApiController]
[Route("v1/games/{id:guid}/reviews")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Reviews")]
public class GameReviewCollectionController : ControllerBase
{
    private readonly IReviewReadingService _readingService;
    private readonly IReviewCreatingService _creatingService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GameReviewCollectionController(
        IReviewReadingService readingService,
        IReviewCreatingService creatingService,
        IMapper mapper)
    {
        _readingService = readingService;
        _creatingService = creatingService;
        _mapper = mapper;
    }

    /// <summary>
    /// Get reviews for a game
    /// </summary>
    /// <remarks>
    /// Returns reviews written about the specified game.
    /// Only participants (GM, assistant, or players with valid characters) can leave game reviews.
    /// </remarks>
    /// <param name="id">Game ID</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">List of game reviews</response>
    /// <response code="404">Game not found</response>
    [HttpGet(Name = nameof(GetGameReviews))]
    [ProducesResponseType(typeof(ListEnvelope<ApiReview>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetGameReviews(Guid id, [FromQuery] PagingQuery q)
    {
        var (reviews, paging) = await _readingService.GetGameReviews(id, q);
        var apiReviews = reviews.Select(_mapper.Map<ApiReview>);
        return Ok(new ListEnvelope<ApiReview>(apiReviews, new Paging(paging)));
    }

    /// <summary>
    /// Create game review
    /// </summary>
    /// <remarks>
    /// Creates a review for the specified game.
    /// You can only review games where you are a participant (GM, assistant, or have a valid character).
    /// Only one review per game is allowed.
    /// </remarks>
    /// <param name="id">Game ID</param>
    /// <param name="request">Review data</param>
    /// <response code="201">Review created successfully</response>
    /// <response code="400">Invalid review data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (not a participant in the game)</response>
    /// <response code="404">Game not found</response>
    /// <response code="409">Review already exists</response>
    [HttpPost(Name = nameof(CreateGameReview))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ApiReview>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    public async Task<IActionResult> CreateGameReview(Guid id, [FromBody] CreateReviewRequest request)
    {
        var review = await _creatingService.CreateGameReview(id, request.Text ?? string.Empty);
        var apiReview = _mapper.Map<ApiReview>(review);
        return CreatedAtRoute(nameof(ReviewController.GetReview), new { id = review.Id }, new Envelope<ApiReview>(apiReview));
    }
}
