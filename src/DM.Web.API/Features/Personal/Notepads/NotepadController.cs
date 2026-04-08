using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Personal.Notepads;

/// <summary>
/// Personal notepad management
/// </summary>
/// <remarks>
/// User notepads allow authenticated users to store personal private notes.
/// </remarks>
[ApiController]
[Route("v1/users/me/notepad")]
[ApiExplorerSettings(GroupName = "Personal")]
[Tags("Notepad")]
[AuthenticationRequired]
public class NotepadController : ControllerBase
{
    private readonly IUserNotepadApiService _notepadApiService;

    /// <inheritdoc />
    public NotepadController(IUserNotepadApiService notepadApiService)
    {
        _notepadApiService = notepadApiService;
    }

    /// <summary>
    /// Get my notepad entries
    /// </summary>
    /// <response code="200">List of notepad entries</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet(Name = nameof(GetMyNotepad))]
    [ProducesResponseType(typeof(ListEnvelope<NotepadEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyNotepad() =>
        Ok(await _notepadApiService.GetEntries());

    /// <summary>
    /// Create notepad entry
    /// </summary>
    /// <param name="request">Entry creation request</param>
    /// <response code="201">Entry created</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPost(Name = nameof(CreateNotepadEntry))]
    [ProducesResponseType(typeof(NotepadEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateNotepadEntry([FromBody] CreateNotepadEntryRequest request)
    {
        var result = await _notepadApiService.CreateEntry(request);
        return CreatedAtRoute(nameof(GetMyNotepadEntry), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get notepad entry by ID
    /// </summary>
    /// <param name="id">Entry identifier</param>
    /// <response code="200">Entry details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User doesn't have access to this entry</response>
    /// <response code="404">Entry not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetMyNotepadEntry))]
    [ProducesResponseType(typeof(NotepadEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyNotepadEntry(Guid id) =>
        Ok(await _notepadApiService.GetEntry(id));

    /// <summary>
    /// Update notepad entry
    /// </summary>
    /// <param name="id">Entry identifier</param>
    /// <param name="request">Update request</param>
    /// <response code="200">Entry updated</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User doesn't have access to this entry</response>
    /// <response code="404">Entry not found</response>
    [HttpPatch("{id:guid}", Name = nameof(UpdateMyNotepadEntry))]
    [ProducesResponseType(typeof(NotepadEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyNotepadEntry(Guid id, [FromBody] UpdateNotepadEntryRequest request) =>
        Ok(await _notepadApiService.UpdateEntry(id, request));

    /// <summary>
    /// Delete notepad entry
    /// </summary>
    /// <param name="id">Entry identifier</param>
    /// <response code="204">Entry deleted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User doesn't have access to this entry</response>
    /// <response code="404">Entry not found</response>
    [HttpDelete("{id:guid}", Name = nameof(DeleteMyNotepadEntry))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMyNotepadEntry(Guid id)
    {
        await _notepadApiService.DeleteEntry(id);
        return NoContent();
    }
}
