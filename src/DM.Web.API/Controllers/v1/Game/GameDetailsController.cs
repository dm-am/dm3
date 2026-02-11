using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;
using DM.Web.API.Dto.Shared;
using DM.Web.API.Services.Game;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Game;

/// <inheritdoc />
[ApiController]
[Route("v1/games")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Game Details")]
public class GameDetailsController : ControllerBase
{
    private readonly IGameApiService _gameApiService;
    private readonly ICommentApiService _commentApiService;
    private readonly ICharacterApiService _characterApiService;

    /// <inheritdoc />
    public GameDetailsController(
        IGameApiService gameApiService,
        ICommentApiService commentApiService,
        ICharacterApiService characterApiService)
    {
        _gameApiService = gameApiService;
        _commentApiService = commentApiService;
        _characterApiService = characterApiService;
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

    /// <summary>
    /// Get game discussion with comments, paging and permission flags
    /// </summary>
    /// <remarks>
    /// Returns a unified discussion view with:
    /// - Paginated comments
    /// - Permission flags (CanEdit, CanDelete, CanLike) for each comment
    /// - Total likes count across all comments
    /// - CanComment flag for the current user
    /// </remarks>
    /// <param name="id">Game identifier</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">Discussion with comments and metadata</response>
    /// <response code="410">Game not found</response>
    [HttpGet("{id}/discussion", Name = nameof(GetGameDiscussion))]
    [ProducesResponseType(typeof(DiscussionResponse), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetGameDiscussion(Guid id, [FromQuery] PagingQuery q) =>
        Ok(await _commentApiService.GetDiscussion(id, q));

    /// <summary>
    /// Mark all characters in game as read
    /// </summary>
    /// <param name="id">Game id</param>
    /// <response code="204">Characters marked as read successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="401">User is not authorized to read characters in this game</response>
    /// <response code="410">Game not found</response>
    [HttpDelete("{id}/characters/unread", Name = nameof(ReadGameCharacters))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> ReadGameCharacters(Guid id)
    {
        await _characterApiService.MarkAsRead(id);
        return NoContent();
    }
}
