using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notepads;
using DM.Web.API.Services.Notepads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Community;

/// <summary>
/// API controller for managing user notepads
/// </summary>
/// <remarks>
/// User notepads allow authenticated users to store personal private notes
/// </remarks>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("User Notepads")]
public class UserNotepadController : ControllerBase
{
    private readonly INotepadApiService _notepadApiService;

    /// <inheritdoc />
    public UserNotepadController(INotepadApiService notepadApiService)
    {
        _notepadApiService = notepadApiService;
    }

    /// <summary>
    /// Get user's personal notepad entries
    /// </summary>
    /// <response code="200">List of notepad entries</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("~/v1/users/me/notepad", Name = nameof(GetUserNotepad))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<NotepadEntry>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetUserNotepad() =>
        Ok(await _notepadApiService.GetUserEntries());

    /// <summary>
    /// Create entry in user's personal notepad
    /// </summary>
    /// <param name="request">Entry creation request</param>
    /// <response code="201">Entry created</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPost("~/v1/users/me/notepad", Name = nameof(CreateUserNotepadEntry))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<NotepadEntry>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> CreateUserNotepadEntry([FromBody] CreateNotepadEntryRequest request)
    {
        var result = await _notepadApiService.CreateUserEntry(request);
        return CreatedAtRoute("GetNotepadEntry", new { entryId = result.Resource.Id }, result);
    }
}
