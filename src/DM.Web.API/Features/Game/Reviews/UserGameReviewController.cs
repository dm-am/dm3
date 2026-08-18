using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// Game reviews of one user - received and written
/// </summary>
/// <remarks>
/// The game-scoped listing next door answers "what was written about this
/// game". These two answer the profile's question instead, "what has this
/// person written and what has been written about their games", which the
/// game-scoped route cannot: it takes a game and there is no game to name.
///
/// Not to be confused with /v1/users/{username}/reviews-shaped routes for post
/// reviews. A post review rates one post; a game review is about a whole game,
/// and the two counters on the profile are separate numbers.
/// </remarks>
[ApiController]
[Route("v1/users/{username}/game-reviews")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Games")]
public class UserGameReviewController : ControllerBase
{
    private readonly IGameReviewApiService _gameReviewApiService;

    /// <inheritdoc />
    public UserGameReviewController(IGameReviewApiService gameReviewApiService)
    {
        _gameReviewApiService = gameReviewApiService;
    }

    /// <summary>
    /// Get game reviews received by a user
    /// </summary>
    /// <remarks>
    /// Returns reviews written about the games this user masters. A game review
    /// is about a game, so the person it lands on is the game's master; reviews
    /// of games the user merely played in belong to their master and are not
    /// listed here.
    /// </remarks>
    /// <param name="username">Game master's username.</param>
    /// <param name="q">Paging, search and sorting.</param>
    /// <response code="200">List of game reviews.</response>
    /// <response code="404">User not found.</response>
    [HttpGet(Name = nameof(GetUserGameReviews))]
    [ProducesResponseType(typeof(ListEnvelope<GameReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserGameReviews(string username, [FromQuery] UserGameReviewsQuery q) =>
        Ok(await _gameReviewApiService.GetReceivedByUser(username, q));

    /// <summary>
    /// Get game reviews written by a user
    /// </summary>
    /// <remarks>
    /// Returns reviews this user is the author of. Symmetric with GET
    /// game-reviews; the same paging parameters.
    /// </remarks>
    /// <param name="username">Review author's username.</param>
    /// <param name="q">Paging, search and sorting.</param>
    /// <response code="200">List of game reviews written by the user.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("/v1/users/{username}/written-game-reviews", Name = nameof(GetWrittenUserGameReviews))]
    [ProducesResponseType(typeof(ListEnvelope<GameReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWrittenUserGameReviews(string username, [FromQuery] UserGameReviewsQuery q) =>
        Ok(await _gameReviewApiService.GetWrittenByUser(username, q));
}
