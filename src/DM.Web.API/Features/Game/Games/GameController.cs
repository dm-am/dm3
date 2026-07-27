using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
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
    /// When the `playerUsername` filter is set, each game additionally carries
    /// `playerCharacters` — that player's characters (name + status).
    /// `playerParticipation` widens the `playerUsername` scope: `Active` (default) matches
    /// only games where the user has an active character, `Any` also matches games where
    /// the user only has retired characters or an application under review (declined
    /// applications never match). Without `playerUsername` the parameter is ignored.
    /// </remarks>
    /// <response code="200">Returns the paginated list of games</response>
    /// <response code="400">Invalid query parameters</response>
    [HttpGet(Name = nameof(GetGames))]
    [ProducesResponseType(typeof(ListEnvelope<Game>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ListEnvelope<GameRef>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
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
    /// Change game status
    /// </summary>
    /// <remarks>
    /// Applies a single status transition on the game state machine:
    /// `Start` (Draft-&gt;Active), `Freeze` (Active-&gt;Closed/frozen),
    /// `Finish` (Active-&gt;Closed/finished), `Close` (Active or frozen-&gt;Closed),
    /// `Reopen` (Closed-&gt;Active). Only the game leads (master or assistant)
    /// may change the status. Illegal transitions are rejected with 400.
    /// </remarks>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="request">Requested status transition</param>
    /// <response code="200">Returns the updated game details</response>
    /// <response code="400">The requested transition is illegal for the current status</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change the status of this game</response>
    /// <response code="404">Game not found</response>
    [HttpPost("{id}/status", Name = nameof(PostGameStatus))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GameDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostGameStatus(string id, [FromBody] GameStatusChangeRequest request)
    {
        var gameId = Guid.TryParse(id, out var guid)
            ? guid
            : (await _gameApiService.GetByPublicId(id)).Resource.Id;
        return Ok(await _gameApiService.ChangeStatus(gameId, request));
    }

    /// <summary>
    /// Change game premoderation state
    /// </summary>
    /// <remarks>
    /// Mentor action: `SendToPremoderation` (AwaitingEdits-&gt;AwaitingApproval)
    /// puts the game back in the review queue and assigns the acting mentor as
    /// curator; `RemoveFromPremoderation` (AwaitingApproval-&gt;Approved) releases
    /// the game so it becomes publicly visible. Requires Mentor role or above.
    /// Illegal transitions are rejected with 400.
    /// </remarks>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="request">Requested premoderation transition</param>
    /// <response code="200">Returns the updated game details</response>
    /// <response code="400">The requested transition is illegal for the current premoderation state</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not a mentor</response>
    /// <response code="404">Game not found</response>
    [HttpPost("{id}/premoderation", Name = nameof(PostGamePremoderation))]
    [RequireRole(UserRole.Mentor)]
    [ProducesResponseType(typeof(Envelope<GameDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostGamePremoderation(string id, [FromBody] GamePremoderationChangeRequest request)
    {
        // Pass the raw id through: the domain resolves the public id via the
        // repository (ungated) after the Mentor gate. Resolving here through the
        // read-gated GetByPublicId would hide a premoderation-pending game from
        // the non-curator mentor this endpoint exists for.
        return Ok(await _gameApiService.ChangePremoderation(id, request));
    }

    /// <summary>
    /// Reset the recruitment start date
    /// </summary>
    /// <remarks>
    /// Admin action: clears the game's recruitment start date
    /// (`recruitmentStartedUtc`). Requires Admin role.
    /// </remarks>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">Returns the updated game details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not an administrator</response>
    /// <response code="404">Game not found</response>
    [HttpPost("{id}/reset-recruitment-date", Name = nameof(PostResetRecruitmentDate))]
    [RequireRole(UserRole.Admin)]
    [ProducesResponseType(typeof(Envelope<GameDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostResetRecruitmentDate(string id)
    {
        var gameId = Guid.TryParse(id, out var guid)
            ? guid
            : (await _gameApiService.GetByPublicId(id)).Resource.Id;
        return Ok(await _gameApiService.ResetRecruitmentDate(gameId));
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
