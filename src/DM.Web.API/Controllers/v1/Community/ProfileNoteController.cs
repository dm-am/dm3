using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Community;

/// <summary>
/// Profile notes management endpoints
/// </summary>
/// <remarks>
/// Profile notes are personal notes about other users.
/// These notes are private and visible only to the note owner.
///
/// ## Use Cases
/// - Remember information about other users
/// - Track interactions with other players
/// - Personal annotations for moderation purposes
/// </remarks>
[ApiController]
[Route("v1/users/{login}/notes")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Profile Notes")]
public class ProfileNoteController : ControllerBase
{
    private readonly IProfileNoteApiService _profileNoteApiService;

    /// <inheritdoc />
    public ProfileNoteController(IProfileNoteApiService profileNoteApiService)
    {
        _profileNoteApiService = profileNoteApiService;
    }

    /// <summary>
    /// Get personal note about a user
    /// </summary>
    /// <remarks>
    /// Returns your personal note about the specified user.
    /// Returns 404 if no note exists.
    /// </remarks>
    /// <param name="login">User login</param>
    /// <response code="200">Note found</response>
    /// <response code="401">Authentication required</response>
    /// <response code="404">User not found or no note exists</response>
    [HttpGet(Name = nameof(GetProfileNote))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ProfileNote>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetProfileNote(string login)
    {
        var note = await _profileNoteApiService.GetNote(login);
        if (note == null)
        {
            return NotFound(new GeneralError("No note found for this user"));
        }
        return Ok(note);
    }

    /// <summary>
    /// Create or update personal note about a user
    /// </summary>
    /// <remarks>
    /// Creates a new note or updates existing note about the specified user.
    /// Maximum note length is 2000 characters.
    /// </remarks>
    /// <param name="login">User login</param>
    /// <param name="request">Note content</param>
    /// <response code="200">Note created or updated</response>
    /// <response code="400">Invalid note data</response>
    /// <response code="401">Authentication required</response>
    /// <response code="404">User not found</response>
    [HttpPut(Name = nameof(UpsertProfileNote))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ProfileNote>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> UpsertProfileNote(string login, [FromBody] ProfileNoteRequest request)
    {
        var note = await _profileNoteApiService.UpsertNote(login, request);
        return Ok(note);
    }

    /// <summary>
    /// Delete personal note about a user
    /// </summary>
    /// <remarks>
    /// Deletes your personal note about the specified user.
    /// Returns 204 even if no note existed.
    /// </remarks>
    /// <param name="login">User login</param>
    /// <response code="204">Note deleted (or no note existed)</response>
    /// <response code="401">Authentication required</response>
    /// <response code="404">User not found</response>
    [HttpDelete(Name = nameof(DeleteProfileNote))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteProfileNote(string login)
    {
        await _profileNoteApiService.DeleteNote(login);
        return NoContent();
    }
}
