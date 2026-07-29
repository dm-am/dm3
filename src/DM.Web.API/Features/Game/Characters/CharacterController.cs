using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Notepads;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.Game.Notepads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Characters;

/// <summary>
/// Character management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for game characters.
/// Characters have customizable attributes, notepads, and can participate in room posts.
/// </remarks>
[ApiController]
[Route("v1/characters")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Characters")]
public class CharacterController : ControllerBase
{
    private readonly ICharacterApiService _characterApiService;
    private readonly IGameNotepadApiService _notepadApiService;
    private readonly IGameApiService _gameApiService;

    /// <summary>
    /// Creates a new instance of CharacterController
    /// </summary>
    public CharacterController(
        ICharacterApiService characterApiService,
        IGameNotepadApiService notepadApiService,
        IGameApiService gameApiService)
    {
        _characterApiService = characterApiService;
        _notepadApiService = notepadApiService;
        _gameApiService = gameApiService;
    }

    private async Task<Guid> ResolveGameId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : (await _gameApiService.GetByPublicId(id)).Resource.Id;

    /// <summary>
    /// Get list of characters in game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">Returns the character list</response>
    /// <response code="404">Game not found</response>
    [HttpGet("~/v1/games/{id}/characters", Name = nameof(GetCharacters))]
    [ProducesResponseType(typeof(ListEnvelope<Character>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCharacters(string id)
    {
        var gameId = await ResolveGameId(id);
        return Ok(await _characterApiService.GetAll(gameId));
    }

    /// <summary>
    /// Mark all characters in game as read
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Game not found</response>
    [HttpDelete("~/v1/games/{id}/characters/unread", Name = nameof(MarkCharactersAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkCharactersAsRead(string id)
    {
        var gameId = await ResolveGameId(id);
        await _characterApiService.MarkAsRead(gameId);
        return NoContent();
    }

    /// <summary>
    /// Create new character
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="character">Character details</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of character properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create a character in this game</response>
    /// <response code="404">Game not found</response>
    [HttpPost("~/v1/games/{id}/characters", Name = nameof(PostCharacter))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<CharacterDetails>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostCharacter(string id, [FromBody] CharacterDetails character)
    {
        var gameId = await ResolveGameId(id);
        var result = await _characterApiService.Create(gameId, character);
        return CreatedAtRoute(nameof(GetCharacter),
            new {id = result.Resource.Id}, result);
    }

    /// <summary>
    /// Get character details
    /// </summary>
    /// <param name="id">Character identifier</param>
    /// <response code="200">Returns character details</response>
    /// <response code="404">Character not found</response>
    [HttpGet("{id}", Name = nameof(GetCharacter))]
    [ProducesResponseType(typeof(Envelope<CharacterDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCharacter(Guid id) => Ok(await _characterApiService.Get(id));

    /// <summary>
    /// Update character
    /// </summary>
    /// <param name="id">Character identifier</param>
    /// <param name="character">Updated character details</param>
    /// <response code="200">Returns the updated character</response>
    /// <response code="400">Some of character changed properties were invalid or passed id was not recognized</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change some properties of this character</response>
    /// <response code="404">Character not found</response>
    [HttpPatch("{id}", Name = nameof(PutCharacter))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<CharacterDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutCharacter(Guid id, [FromBody] CharacterDetails character) =>
        Ok(await _characterApiService.Update(id, character));

    /// <summary>
    /// Delete character
    /// </summary>
    /// <param name="id">Character identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove the character</response>
    /// <response code="404">Character not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteCharacter))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCharacter(Guid id)
    {
        await _characterApiService.Delete(id);
        return NoContent();
    }

    #region Character Notepad

    /// <summary>
    /// Get player notepad entries for a character
    /// </summary>
    /// <param name="id">Character identifier</param>
    /// <response code="200">List of notepad entries</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must own the character or be master/assistant</response>
    /// <response code="404">Character not found</response>
    [HttpGet("{id}/notepad", Name = nameof(GetCharacterNotepad))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<NotepadEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCharacterNotepad(Guid id) =>
        Ok(await _notepadApiService.GetCharacterNotepadEntries(id));

    /// <summary>
    /// Create entry in character notepad
    /// </summary>
    /// <param name="id">Character identifier</param>
    /// <param name="request">Entry creation request</param>
    /// <response code="201">Entry created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must own the character or be master/assistant</response>
    /// <response code="404">Character not found</response>
    [HttpPost("{id}/notepad", Name = nameof(CreateCharacterNotepadEntry))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<NotepadEntryResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCharacterNotepadEntry(Guid id, [FromBody] CreateNotepadEntryRequest request)
    {
        var result = await _notepadApiService.CreateCharacterNotepadEntry(id, request);
        return CreatedAtRoute(nameof(GetCharacterNotepadEntry), new { id, entryId = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get character notepad entry by ID
    /// </summary>
    /// <param name="id">Character identifier</param>
    /// <param name="entryId">Entry identifier</param>
    /// <response code="200">Entry details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must own the character or be master/assistant</response>
    /// <response code="404">Entry not found</response>
    [HttpGet("{id}/notepad/{entryId:guid}", Name = nameof(GetCharacterNotepadEntry))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<NotepadEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCharacterNotepadEntry(Guid id, Guid entryId) =>
        Ok(await _notepadApiService.GetEntry(entryId));

    /// <summary>
    /// Update character notepad entry
    /// </summary>
    /// <param name="id">Character identifier</param>
    /// <param name="entryId">Entry identifier</param>
    /// <param name="request">Update request</param>
    /// <response code="200">Entry updated</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must own the character or be master/assistant</response>
    /// <response code="404">Entry not found</response>
    [HttpPatch("{id}/notepad/{entryId:guid}", Name = nameof(UpdateCharacterNotepadEntry))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<NotepadEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCharacterNotepadEntry(Guid id, Guid entryId, [FromBody] UpdateNotepadEntryRequest request) =>
        Ok(await _notepadApiService.UpdateEntry(entryId, request));

    /// <summary>
    /// Delete character notepad entry
    /// </summary>
    /// <param name="id">Character identifier</param>
    /// <param name="entryId">Entry identifier</param>
    /// <response code="204">Entry deleted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must own the character or be master/assistant</response>
    /// <response code="404">Entry not found</response>
    [HttpDelete("{id}/notepad/{entryId:guid}", Name = nameof(DeleteCharacterNotepadEntry))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCharacterNotepadEntry(Guid id, Guid entryId)
    {
        await _notepadApiService.DeleteEntry(entryId);
        return NoContent();
    }

    #endregion
}
