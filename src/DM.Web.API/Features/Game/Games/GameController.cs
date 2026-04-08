using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Game management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for text-based role-playing games.
/// Games contain rooms, characters, posts and support various access levels.
/// </remarks>
[ApiController]
[Route("v1/games")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Games")]
public class GameController : ControllerBase
{
    private readonly IGameApiService _gameApiService;

    /// <summary>
    /// Creates a new instance of GameController
    /// </summary>
    public GameController(IGameApiService gameApiService)
    {
        _gameApiService = gameApiService;
    }

    /// <summary>
    /// Get list of games
    /// </summary>
    /// <param name="q">Query parameters</param>
    /// <remarks>
    /// Use `projection=ref` for lightweight sidebar/menu data (counts instead of user arrays).
    /// Default projection returns full Game with players/readers arrays for table tooltips.
    /// </remarks>
    /// <response code="200">Returns the paginated list of games</response>
    [HttpGet(Name = nameof(GetGames))]
    [ProducesResponseType(typeof(ListEnvelope<Game>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ListEnvelope<GameRef>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGames([FromQuery] GamesQuery q)
    {
        // Response contains user-specific unread counts, cannot use public cache
        Response.Headers.CacheControl = "private, no-store";

        // Return lightweight refs for sidebars, full games for table
        if (string.Equals(q.Projection, "ref", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(await _gameApiService.GetRefs(q));
        }

        return Ok(await _gameApiService.Get(q));
    }

    /// <summary>
    /// Get list of all game tags
    /// </summary>
    /// <response code="200">Returns the list of all game tags</response>
    [HttpGet("tags", Name = nameof(GetTags))]
    [ProducesResponseType(typeof(ListEnvelope<Tag>), StatusCodes.Status200OK)]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetTags()
    {
        return Ok(await _gameApiService.GetTags());
    }

    /// <summary>
    /// Get game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">Returns the game details</response>
    /// <response code="404">Game not found</response>
    [HttpGet("{id}", Name = nameof(GetGame))]
    [ProducesResponseType(typeof(Envelope<Game>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGame(string id)
    {
        if (Guid.TryParse(id, out var guid))
            return Ok(await _gameApiService.Get(guid));
        return Ok(await _gameApiService.GetByPublicId(id));
    }

    /// <summary>
    /// Create new game
    /// </summary>
    /// <param name="request">Game creation data</param>
    /// <response code="201">Game created successfully</response>
    /// <response code="400">Some of game properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create a game</response>
    /// <response code="404">Game not found</response>
    [HttpPost(Name = nameof(PostGame))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GameDetails>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostGame([FromBody] CreateGameRequest request)
    {
        var result = await _gameApiService.Create(request);
        return CreatedAtRoute(nameof(GetGameDetails), new {id = result.Resource.Id}, result);
    }

    /// <summary>
    /// Delete game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="204">Game deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove the game</response>
    /// <response code="404">Game not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteGame))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGame(string id)
    {
        var gameId = Guid.TryParse(id, out var guid)
            ? guid
            : (await _gameApiService.GetByPublicId(id)).Resource.Id;
        await _gameApiService.Delete(gameId);
        return NoContent();
    }

    /// <summary>
    /// Get game details
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">Returns the detailed game information</response>
    /// <response code="404">Game not found</response>
    [HttpGet("{id}/details", Name = nameof(GetGameDetails))]
    [ProducesResponseType(typeof(Envelope<GameDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameDetails(string id)
    {
        if (Guid.TryParse(id, out var guid))
            return Ok(await _gameApiService.GetDetails(guid));
        return Ok(await _gameApiService.GetDetailsByPublicId(id));
    }

    /// <summary>
    /// Update game details
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="game">Game details</param>
    /// <response code="200">Returns the updated game details</response>
    /// <response code="400">Some of game properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change some properties of this game</response>
    /// <response code="404">Game not found</response>
    [HttpPatch("{id}/details", Name = nameof(PatchGameDetails))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GameDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchGameDetails(string id, [FromBody] GameDetails game)
    {
        var gameId = Guid.TryParse(id, out var guid)
            ? guid
            : (await _gameApiService.GetByPublicId(id)).Resource.Id;
        return Ok(await _gameApiService.Update(gameId, game));
    }

    /// <summary>
    /// Get game notes
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">Returns the game notes</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to read notes of this game</response>
    /// <response code="404">Game not found</response>
    [HttpGet("{id}/notes", Name = nameof(GetGameNotes))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GameNotes>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameNotes(string id)
    {
        var gameId = Guid.TryParse(id, out var guid)
            ? guid
            : (await _gameApiService.GetByPublicId(id)).Resource.Id;
        return Ok(await _gameApiService.GetNotes(gameId));
    }

    /// <summary>
    /// Update game notes
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="notes">Game notes</param>
    /// <response code="200">Returns the updated game notes</response>
    /// <response code="400">Some of game properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change notes of this game</response>
    /// <response code="404">Game not found</response>
    [HttpPatch("{id}/notes", Name = nameof(PatchGameNotes))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GameNotes>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchGameNotes(string id, [FromBody] GameNotes notes)
    {
        var gameId = Guid.TryParse(id, out var guid)
            ? guid
            : (await _gameApiService.GetByPublicId(id)).Resource.Id;
        return Ok(await _gameApiService.UpdateNotes(gameId, notes));
    }
}
