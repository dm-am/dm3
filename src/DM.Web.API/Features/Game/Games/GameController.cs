using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Games;

/// <inheritdoc />
[ApiController]
[Route("v1/games")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Games")]
public class GameController : ControllerBase
{
    private readonly IGameApiService _gameApiService;

    /// <inheritdoc />
    public GameController(IGameApiService gameApiService)
    {
        _gameApiService = gameApiService;
    }

    /// <summary>
    /// Get list of games
    /// </summary>
    /// <response code="200">Returns the paginated list of games</response>
    [HttpGet(Name = nameof(GetGames))]
    [ProducesResponseType(typeof(ListEnvelope<Game>), 200)]
    public async Task<IActionResult> GetGames([FromQuery] GamesQuery q)
    {
        Response.Headers.CacheControl = "public, max-age=30";
        return Ok(await _gameApiService.Get(q));
    }

    /// <summary>
    /// Get list of games owned by current user
    /// </summary>
    /// <response code="200">Returns the list of games owned by the authenticated user</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("owned", Name = nameof(GetOwnGames))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Game>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetOwnGames() => Ok(await _gameApiService.GetOwn());

    /// <summary>
    /// Get list of 10 most popular games by readers
    /// </summary>
    /// <response code="200">Returns the list of 10 most popular games</response>
    [HttpGet("popular", Name = nameof(GetPopularGames))]
    [ProducesResponseType(typeof(ListEnvelope<Game>), 200)]
    public async Task<IActionResult> GetPopularGames()
    {
        Response.Headers.CacheControl = "public, max-age=60";
        return Ok(await _gameApiService.GetPopular());
    }

    /// <summary>
    /// Get list of all game tags
    /// </summary>
    /// <response code="200">Returns the list of all game tags</response>
    [HttpGet("tags", Name = nameof(GetTags))]
    [ProducesResponseType(typeof(ListEnvelope<Tag>), 200)]
    public async Task<IActionResult> GetTags()
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(await _gameApiService.GetTags());
    }

    /// <summary>
    /// Get game
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <response code="200">Returns the game details</response>
    /// <response code="410">Game not found</response>
    [HttpGet("{id}", Name = nameof(GetGame))]
    [ProducesResponseType(typeof(Envelope<Game>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetGame(Guid id) => Ok(await _gameApiService.Get(id));

    /// <summary>
    /// Create new game
    /// </summary>
    /// <param name="request">Game creation data</param>
    /// <response code="201">Game created successfully</response>
    /// <response code="400">Some of game properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create a game</response>
    /// <response code="410">Game not found</response>
    [HttpPost(Name = nameof(PostGame))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GameDetails>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PostGame([FromBody] CreateGameRequest request)
    {
        var result = await _gameApiService.Create(request);
        return CreatedAtRoute(nameof(GetGameDetails), new {id = result.Resource.Id}, result);
    }

    /// <summary>
    /// Delete game
    /// </summary>
    /// <response code="204">Game deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove the game</response>
    /// <response code="410">Game not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteGame))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteGame(Guid id)
    {
        await _gameApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Get game details
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <response code="200">Returns the detailed game information</response>
    /// <response code="410">Game not found</response>
    [HttpGet("{id}/details", Name = nameof(GetGameDetails))]
    [ProducesResponseType(typeof(Envelope<GameDetails>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetGameDetails(Guid id) => Ok(await _gameApiService.GetDetails(id));

    /// <summary>
    /// Update game details
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <param name="game">Game details</param>
    /// <response code="200">Returns the updated game details</response>
    /// <response code="400">Some of game properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change some properties of this game</response>
    /// <response code="410">Game not found</response>
    [HttpPatch("{id}/details", Name = nameof(PatchGameDetails))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GameDetails>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PatchGameDetails(Guid id, [FromBody] GameDetails game) =>
        Ok(await _gameApiService.Update(id, game));

    /// <summary>
    /// Get game notes
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <response code="200">Returns the game notes</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to read notes of this game</response>
    /// <response code="410">Game not found</response>
    [HttpGet("{id}/notes", Name = nameof(GetGameNotes))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GameNotes>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetGameNotes(Guid id) => Ok(await _gameApiService.GetNotes(id));

    /// <summary>
    /// Update game notes
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <param name="notes">Game notes</param>
    /// <response code="200">Returns the updated game notes</response>
    /// <response code="400">Some of game properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change notes of this game</response>
    /// <response code="410">Game not found</response>
    [HttpPatch("{id}/notes", Name = nameof(PatchGameNotes))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GameNotes>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PatchGameNotes(Guid id, [FromBody] GameNotes notes) =>
        Ok(await _gameApiService.UpdateNotes(id, notes));
}
