using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Notepads;
using DM.Web.API.Features.Game.Games;
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
[Route("v1/games/{id}/notepad")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Game Notepads")]
[AuthenticationRequired]
public class GameNotepadController : ControllerBase
{
    private readonly IGameNotepadApiService _notepadApiService;
    private readonly IGameApiService _gameApiService;

    /// <inheritdoc />
    public GameNotepadController(
        IGameNotepadApiService notepadApiService,
        IGameApiService gameApiService)
    {
        _notepadApiService = notepadApiService;
        _gameApiService = gameApiService;
    }

    /// <summary>
    /// Get game master notepad entries
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">List of notepad entries</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    [HttpGet(Name = nameof(GetGameMasterNotepad))]
    [ProducesResponseType(typeof(ListEnvelope<NotepadEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetGameMasterNotepad(string id)
    {
        var resolvedId = await _gameApiService.ResolveId(id);
        return Ok(await _notepadApiService.GetMasterEntries(resolvedId));
    }

    /// <summary>
    /// Create entry in game master notepad
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="request">Entry creation request</param>
    /// <response code="201">Entry created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    [HttpPost(Name = nameof(CreateGameMasterNotepadEntry))]
    [ProducesResponseType(typeof(Envelope<NotepadEntryResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateGameMasterNotepadEntry(string id, [FromBody] CreateNotepadEntryRequest request)
    {
        var resolvedId = await _gameApiService.ResolveId(id);
        var result = await _notepadApiService.CreateMasterEntry(resolvedId, request);
        return CreatedAtRoute(nameof(GetGameMasterNotepadEntry), new { id, entryId = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get game master notepad entry by ID
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="entryId">Entry identifier</param>
    /// <response code="200">Entry details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    /// <response code="404">Entry not found</response>
    [HttpGet("{entryId:guid}", Name = nameof(GetGameMasterNotepadEntry))]
    [ProducesResponseType(typeof(Envelope<NotepadEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameMasterNotepadEntry(string id, Guid entryId) =>
        Ok(await _notepadApiService.GetEntry(entryId));

    /// <summary>
    /// Update game master notepad entry
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="entryId">Entry identifier</param>
    /// <param name="request">Update request</param>
    /// <response code="200">Entry updated</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    /// <response code="404">Entry not found</response>
    [HttpPatch("{entryId:guid}", Name = nameof(UpdateGameMasterNotepadEntry))]
    [ProducesResponseType(typeof(Envelope<NotepadEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGameMasterNotepadEntry(string id, Guid entryId, [FromBody] UpdateNotepadEntryRequest request) =>
        Ok(await _notepadApiService.UpdateEntry(entryId, request));

    /// <summary>
    /// Delete game master notepad entry
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="entryId">Entry identifier</param>
    /// <response code="204">Entry deleted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be master or assistant</response>
    /// <response code="404">Entry not found</response>
    [HttpDelete("{entryId:guid}", Name = nameof(DeleteGameMasterNotepadEntry))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGameMasterNotepadEntry(string id, Guid entryId)
    {
        await _notepadApiService.DeleteEntry(entryId);
        return NoContent();
    }
}
