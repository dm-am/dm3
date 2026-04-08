using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.Game.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Readers;

/// <summary>
/// Game readers (subscribers) API
/// </summary>
/// <remarks>
/// Provides endpoints for viewing and managing game readers (subscribers).
/// Readers are users subscribed to the game for updates without participating as players.
/// </remarks>
[ApiController]
[Route("v1/games/{id}/readers")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Readers")]
public class GameReaderController : ControllerBase
{
    private readonly IGameUserApiService _userApiService;
    private readonly IGameApiService _gameApiService;

    /// <inheritdoc />
    public GameReaderController(
        IGameUserApiService userApiService,
        IGameApiService gameApiService)
    {
        _userApiService = userApiService;
        _gameApiService = gameApiService;
    }

    private async Task<Guid> ResolveGameId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : (await _gameApiService.GetByPublicId(id)).Resource.Id;

    /// <summary>
    /// Get list of game readers
    /// </summary>
    /// <remarks>
    /// Returns users subscribed to the game without other roles.
    /// </remarks>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">List of readers</response>
    /// <response code="404">Game not found</response>
    [HttpGet(Name = nameof(GetGameReaders))]
    [ProducesResponseType(typeof(ListEnvelope<GameUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameReaders(string id)
    {
        var gameId = await ResolveGameId(id);
        var readers = await _userApiService.GetReaders(gameId);
        return Ok(new ListEnvelope<GameUser>(readers));
    }

    /// <summary>
    /// Subscribe to the game as a reader
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="201">Successfully subscribed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to subscribe</response>
    /// <response code="404">Game not found</response>
    /// <response code="409">Already subscribed</response>
    [HttpPost(Name = nameof(SubscribeToGame))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(GameUser), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubscribeToGame(string id)
    {
        var gameId = await ResolveGameId(id);
        var reader = await _userApiService.Subscribe(gameId);
        return CreatedAtRoute(nameof(GetGameReaders), new { id }, reader);
    }

    /// <summary>
    /// Unsubscribe from the game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="204">Successfully unsubscribed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Game not found or not subscribed</response>
    [HttpDelete(Name = nameof(UnsubscribeFromGame))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnsubscribeFromGame(string id)
    {
        var gameId = await ResolveGameId(id);
        await _userApiService.Unsubscribe(gameId);
        return NoContent();
    }

    // Note: DELETE /{username} is not provided - readers can only unsubscribe themselves
    // Use blacklist to prevent problematic users from accessing content
}
