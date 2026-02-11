using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notepads;
using DM.Web.API.Services.Notepads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Common;

/// <summary>
/// API controller for managing individual notepad entries
/// </summary>
/// <remarks>
/// Provides operations for reading, updating, and deleting notepad entries across all notepad types
/// </remarks>
[ApiController]
[Route("v1/notepad-entries")]
[ApiExplorerSettings(GroupName = "Common")]
[Tags("Notepad Entries")]
public class NotepadEntryController : ControllerBase
{
    private readonly INotepadApiService _notepadApiService;

    /// <inheritdoc />
    public NotepadEntryController(INotepadApiService notepadApiService)
    {
        _notepadApiService = notepadApiService;
    }

    /// <summary>
    /// Get notepad entry by ID
    /// </summary>
    /// <param name="id">Entry identifier</param>
    /// <response code="200">Entry details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User doesn't have access to this entry</response>
    /// <response code="404">Entry not found</response>
    [HttpGet("{id}", Name = nameof(GetNotepadEntry))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<NotepadEntry>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetNotepadEntry(Guid id) =>
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
    [HttpPatch("{id}", Name = nameof(UpdateNotepadEntry))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<NotepadEntry>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> UpdateNotepadEntry(Guid id, [FromBody] UpdateNotepadEntryRequest request) =>
        Ok(await _notepadApiService.UpdateEntry(id, request));

    /// <summary>
    /// Delete notepad entry
    /// </summary>
    /// <param name="id">Entry identifier</param>
    /// <response code="204">Entry deleted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User doesn't have access to this entry</response>
    [HttpDelete("{id}", Name = nameof(DeleteNotepadEntry))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    public async Task<IActionResult> DeleteNotepadEntry(Guid id)
    {
        await _notepadApiService.DeleteEntry(id);
        return NoContent();
    }
}
