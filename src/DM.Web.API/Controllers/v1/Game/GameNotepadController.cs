using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Controllers.v1.Common;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notepads;
using DM.Web.API.Services.Notepads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Game;

/// <summary>
/// API controller for managing game master notepads
/// </summary>
/// <remarks>
/// Game master notepads are shared notes for game masters and assistants.
/// For character (player) notepads, see CharacterController.
/// </remarks>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Game Notepads")]
public class GameNotepadController : ControllerBase
{
    private readonly INotepadApiService _notepadApiService;

    /// <inheritdoc />
    public GameNotepadController(INotepadApiService notepadApiService)
    {
        _notepadApiService = notepadApiService;
    }

    #region Game Master Notepad

    /// <summary>
    /// Get game master notepad entries
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <response code="200">List of notepad entries</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    [HttpGet("games/{id}/notepad", Name = nameof(GetGameMasterNotepad))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<NotepadEntry>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    public async Task<IActionResult> GetGameMasterNotepad(Guid id) =>
        Ok(await _notepadApiService.GetGameMasterEntries(id));

    /// <summary>
    /// Create entry in game master notepad
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <param name="request">Entry creation request</param>
    /// <response code="201">Entry created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    [HttpPost("games/{id}/notepad", Name = nameof(CreateGameMasterNotepadEntry))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<NotepadEntry>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    public async Task<IActionResult> CreateGameMasterNotepadEntry(Guid id, [FromBody] CreateNotepadEntryRequest request)
    {
        var result = await _notepadApiService.CreateGameMasterEntry(id, request);
        return CreatedAtRoute(nameof(NotepadEntryController.GetNotepadEntry), new { id = result.Resource.Id }, result);
    }

    #endregion
}
