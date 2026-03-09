using DM.Web.API.Shared.Authentication;
using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Game.Features.Reviews;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ApiReview = DM.Web.API.Features.Community.Reviews.Review;
using CreateReviewRequest = DM.Web.API.Features.Community.Reviews.CreateReviewRequest;
using ReviewController = DM.Web.API.Features.Community.Reviews.ReviewController;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// Game reviews collection - list and create
/// </summary>
[ApiController]
[Route("v1/games/{id:guid}/reviews")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Games")]
public class GameReviewController : ControllerBase
{
    private readonly IGameReviewService _gameReviewService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GameReviewController(
        IGameReviewService gameReviewService,
        IMapper mapper)
    {
        _gameReviewService = gameReviewService;
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
    [ProducesResponseType(typeof(ErrorEnvelope), 404)]
    public async Task<IActionResult> GetGameReviews(Guid id, [FromQuery] PagingQuery q)
    {
        var (reviews, paging) = await _gameReviewService.GetListAsync(id, q);
        var apiReviews = reviews.Select(_mapper.Map<ApiReview>);
        return Ok(new ListEnvelope<ApiReview>(apiReviews, new PagingInfo(paging)));
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
    [ProducesResponseType(typeof(ApiReview), 201)]
    [ProducesResponseType(typeof(ErrorEnvelope), 400)]
    [ProducesResponseType(typeof(ErrorEnvelope), 401)]
    [ProducesResponseType(typeof(ErrorEnvelope), 403)]
    [ProducesResponseType(typeof(ErrorEnvelope), 404)]
    [ProducesResponseType(typeof(ErrorEnvelope), 409)]
    public async Task<IActionResult> CreateGameReview(Guid id, [FromBody] CreateReviewRequest request)
    {
        var createReview = new CreateGameReview
        {
            GameId = id,
            Text = request.Text ?? string.Empty
        };
        var review = await _gameReviewService.CreateAsync(createReview);
        var apiReview = _mapper.Map<ApiReview>(review);
        return CreatedAtRoute(nameof(ReviewController.GetReview), new { id = review.Id }, apiReview);
    }
}
