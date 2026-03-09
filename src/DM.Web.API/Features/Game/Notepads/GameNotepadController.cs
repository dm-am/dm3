using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Notepads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Notepads;

/// <summary>
/// API controller for managing game master notepads
/// </summary>
/// <remarks>
/// Game master notepads are shared notes for game masters and assistants.
/// For character (player) notepads, see CharacterController.
/// </remarks>
[ApiController]
[Route("v1/games/{gameId}/notepad")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Game Notepads")]
[AuthenticationRequired]
public class GameNotepadController : ControllerBase
{
    private readonly IGameNotepadApiService _notepadApiService;

    /// <inheritdoc />
    public GameNotepadController(IGameNotepadApiService notepadApiService)
    {
        _notepadApiService = notepadApiService;
    }

    /// <summary>
    /// Get game master notepad entries
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <response code="200">List of notepad entries</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    [HttpGet(Name = nameof(GetGameMasterNotepad))]
    [ProducesResponseType(typeof(ListEnvelope<NotepadEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetGameMasterNotepad(Guid gameId) =>
        Ok(await _notepadApiService.GetMasterEntries(gameId));

    /// <summary>
    /// Create entry in game master notepad
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="request">Entry creation request</param>
    /// <response code="201">Entry created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    [HttpPost(Name = nameof(CreateGameMasterNotepadEntry))]
    [ProducesResponseType(typeof(Envelope<NotepadEntryResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateGameMasterNotepadEntry(Guid gameId, [FromBody] CreateNotepadEntryRequest request)
    {
        var result = await _notepadApiService.CreateMasterEntry(gameId, request);
        return CreatedAtRoute(nameof(GetGameMasterNotepadEntry), new { gameId, entryId = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get game master notepad entry by ID
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="entryId">Entry identifier</param>
    /// <response code="200">Entry details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    /// <response code="404">Entry not found</response>
    [HttpGet("{entryId:guid}", Name = nameof(GetGameMasterNotepadEntry))]
    [ProducesResponseType(typeof(NotepadEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameMasterNotepadEntry(Guid gameId, Guid entryId) =>
        Ok(await _notepadApiService.GetEntry(entryId));

    /// <summary>
    /// Update game master notepad entry
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="entryId">Entry identifier</param>
    /// <param name="request">Update request</param>
    /// <response code="200">Entry updated</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    /// <response code="404">Entry not found</response>
    [HttpPatch("{entryId:guid}", Name = nameof(UpdateGameMasterNotepadEntry))]
    [ProducesResponseType(typeof(NotepadEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGameMasterNotepadEntry(Guid gameId, Guid entryId, [FromBody] UpdateNotepadEntryRequest request) =>
        Ok(await _notepadApiService.UpdateEntry(entryId, request));

    /// <summary>
    /// Delete game master notepad entry
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="entryId">Entry identifier</param>
    /// <response code="204">Entry deleted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    [HttpDelete("{entryId:guid}", Name = nameof(DeleteGameMasterNotepadEntry))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteGameMasterNotepadEntry(Guid gameId, Guid entryId)
    {
        await _notepadApiService.DeleteEntry(entryId);
        return NoContent();
    }
}
