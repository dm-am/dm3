using DM.Web.API.Shared.Authentication;
using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
    [ProducesResponseType(typeof(ListEnvelope<GameReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameReviews(Guid id, [FromQuery] PagingQuery q)
    {
        var (reviews, paging) = await _gameReviewService.GetListAsync(id, q);
        var apiReviews = reviews.Select(_mapper.Map<GameReviewDto>);
        return Ok(new ListEnvelope<GameReviewDto>(apiReviews, new PagingInfo(paging)));
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
    [ProducesResponseType(typeof(Envelope<GameReviewDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateGameReview(Guid id, [FromBody] CreateGameReviewRequest request)
    {
        var createReview = new CreateGameReview
        {
            GameId = id,
            Text = request.Text
        };
        var review = await _gameReviewService.CreateAsync(createReview);
        var apiReview = _mapper.Map<GameReviewDto>(review);
        return CreatedAtRoute(nameof(GetGameReview), new { id = review.GameId, reviewId = review.Id }, new Envelope<GameReviewDto>(apiReview));
    }

    /// <summary>
    /// Get single game review by ID
    /// </summary>
    /// <param name="id">Game ID</param>
    /// <param name="reviewId">Review ID</param>
    /// <response code="200">Game review</response>
    /// <response code="404">Review not found</response>
    [HttpGet("{reviewId:guid}", Name = nameof(GetGameReview))]
    [ProducesResponseType(typeof(Envelope<GameReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameReview(Guid id, Guid reviewId)
    {
        var review = await _gameReviewService.GetAsync(reviewId);
        return Ok(new Envelope<GameReviewDto>(_mapper.Map<GameReviewDto>(review)));
    }
}
