using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Game;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Game;

/// <inheritdoc />
[ApiController]
[Route("v1/games")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Game Readers")]
public class GameReaderController : ControllerBase
{
    private readonly IReaderApiService _readerApiService;

    /// <inheritdoc />
    public GameReaderController(IReaderApiService readerApiService)
    {
        _readerApiService = readerApiService;
    }

    /// <summary>
    /// Get list of all game readers
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <response code="200">Returns the list of readers subscribed to the game</response>
    /// <response code="410">Game not found</response>
    [HttpGet("{id}/readers", Name = nameof(GetReaders))]
    [ProducesResponseType(typeof(ListEnvelope<User>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetReaders(Guid id) => Ok(await _readerApiService.Get(id));

    /// <summary>
    /// Subscribe to the game as a reader
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <response code="201">User successfully subscribed to the game as reader</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to subscribe to this game</response>
    /// <response code="409">User is already subscribed to this game</response>
    /// <response code="410">Game not found</response>
    [HttpPost("{id}/readers", Name = nameof(PostReader))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PostReader(Guid id) =>
        CreatedAtRoute("GetGame", new {id}, await _readerApiService.Subscribe(id));

    /// <summary>
    /// Unsubscribe from the game as a reader
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <response code="201">User successfully unsubscribed from the game</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to unsubscribe from this game</response>
    /// <response code="409">User is not subscribed to this game</response>
    /// <response code="410">Game not found</response>
    [HttpDelete("{id}/readers", Name = nameof(DeleteReader))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteReader(Guid id)
    {
        await _readerApiService.Unsubscribe(id);
        return NoContent();
    }
}
