using DM.Web.API.Shared.Authentication;
using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// Game reviews of one game - list, read, create, edit and delete
/// </summary>
/// <remarks>
/// The game segment takes the same identifier as every other subroute of a game:
/// the five-letter public id or a GUID. It used to be declared {id:guid}, alone
/// among the 33 published paths under /v1/games, so a client holding the public
/// id off the page URL got 200 from /characters and a bodiless routing 404 from
/// /reviews, and had to spend a round trip on GET /v1/games/{publicId} to learn
/// a GUID the rest of the API never asks for.
/// </remarks>
[ApiController]
[Route("v1/games/{id}/reviews")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Games")]
public class GameReviewController : ControllerBase
{
    private readonly IGameReviewApiService _gameReviewApiService;
    private readonly IGameApiService _gameApiService;

    /// <inheritdoc />
    public GameReviewController(
        IGameReviewApiService gameReviewApiService,
        IGameApiService gameApiService)
    {
        _gameReviewApiService = gameReviewApiService;
        _gameApiService = gameApiService;
    }

    /// <summary>
    /// Get reviews for a game
    /// </summary>
    /// <remarks>
    /// Returns reviews written about the specified game.
    /// Only participants (GM, assistant, or players with valid characters) can leave game reviews.
    /// </remarks>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">List of game reviews</response>
    /// <response code="404">Game not found</response>
    [HttpGet(Name = nameof(GetGameReviews))]
    [ProducesResponseType(typeof(ListEnvelope<GameReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameReviews(string id, [FromQuery] PagingQuery q)
    {
        var gameId = await _gameApiService.ResolveId(id);
        return Ok(await _gameReviewApiService.GetList(gameId, q));
    }

    /// <summary>
    /// Create game review
    /// </summary>
    /// <remarks>
    /// Creates a review for the specified game.
    /// You can only review games where you are a participant (GM, assistant, or have a valid character).
    /// Only one review per game is allowed.
    /// </remarks>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
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
    public async Task<IActionResult> CreateGameReview(string id, [FromBody] CreateGameReviewRequest request)
    {
        var gameId = await _gameApiService.ResolveId(id);
        var result = await _gameReviewApiService.Create(gameId, request);
        return CreatedAtRoute(nameof(GetGameReview),
            new { id = result.Resource.GameId, reviewId = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get single game review by ID
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="reviewId">Review ID</param>
    /// <response code="200">Game review</response>
    /// <response code="404">Game not found, or the review is not a review of this game</response>
    [HttpGet("{reviewId:guid}", Name = nameof(GetGameReview))]
    [ProducesResponseType(typeof(Envelope<GameReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameReview(string id, Guid reviewId) =>
        Ok(await _gameReviewApiService.Get(await _gameApiService.ResolveId(id), reviewId));

    /// <summary>
    /// Update game review
    /// </summary>
    /// <remarks>
    /// Changes the text; an omitted text keeps the stored one.
    ///
    /// **Requirements:**
    /// - Only the review's author may edit it. A moderator does not rewrite
    ///   somebody else's opinion — the moderation tool is DELETE below
    /// - The window is 24 hours from publication (admins are exempt)
    /// </remarks>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="reviewId">Review ID</param>
    /// <param name="request">Fields to change</param>
    /// <response code="200">Review updated</response>
    /// <response code="400">Invalid update data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (not the author, window expired)</response>
    /// <response code="404">Review not found</response>
    [HttpPatch("{reviewId:guid}", Name = nameof(UpdateGameReview))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GameReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGameReview(
        string id, Guid reviewId, [FromBody] UpdateGameReviewRequest request) =>
        Ok(await _gameReviewApiService.Update(reviewId, request));

    /// <summary>
    /// Delete game review
    /// </summary>
    /// <remarks>
    /// Soft-deletes the review.
    ///
    /// **Requirements:**
    /// - The author, at any time — no window
    /// - Or a senior moderator, on somebody else's review: taking down a stated
    ///   opinion sits a step above deleting a comment, and AUTHORIZATION.md puts
    ///   it with profile moderation
    /// </remarks>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="reviewId">Review ID</param>
    /// <response code="204">Review deleted</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (neither the author nor a senior moderator)</response>
    /// <response code="404">Review not found</response>
    [HttpDelete("{reviewId:guid}", Name = nameof(DeleteGameReview))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGameReview(string id, Guid reviewId)
    {
        await _gameReviewApiService.Delete(reviewId);
        return NoContent();
    }
}
