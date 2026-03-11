using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Personal.ProfileNotes;

/// <summary>
/// Personal notes about other users
/// </summary>
/// <remarks>
/// User profile notes are personal notes about other users.
/// These notes are private and visible only to the note owner.
///
/// ## Use Cases
/// - Remember information about other users
/// - Track interactions with other players
/// - Personal annotations for moderation purposes
/// </remarks>
[ApiController]
[Route("v1/users/me/notes")]
[ApiExplorerSettings(GroupName = "Personal")]
[Tags("ProfileNotes")]
[AuthenticationRequired]
public class UserProfileNoteController : ControllerBase
{
    private readonly IUserProfileNoteApiService _userProfileNoteApiService;

    /// <inheritdoc />
    public UserProfileNoteController(IUserProfileNoteApiService userProfileNoteApiService)
    {
        _userProfileNoteApiService = userProfileNoteApiService;
    }

    /// <summary>
    /// Get my note about a user
    /// </summary>
    /// <remarks>
    /// Returns your personal note about the specified user.
    /// Returns 404 if no note exists.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <response code="200">Note found</response>
    /// <response code="401">Authentication required</response>
    /// <response code="404">User not found or no note exists</response>
    [HttpGet("{username}", Name = nameof(GetMyNoteAboutUser))]
    [ProducesResponseType(typeof(UserProfileNote), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyNoteAboutUser(string username)
    {
        var note = await _userProfileNoteApiService.GetNote(username);
        if (note == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "No note found for this user");
        }
        return Ok(note);
    }

    /// <summary>
    /// Create or update note about a user
    /// </summary>
    /// <remarks>
    /// Creates a new note or updates existing note about the specified user.
    /// If the text is empty or whitespace, the note is deleted and 204 No Content is returned.
    /// Maximum note length is 2000 characters.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <param name="request">Note content</param>
    /// <response code="200">Note created or updated</response>
    /// <response code="204">Note deleted (empty text provided)</response>
    /// <response code="400">Invalid note data</response>
    /// <response code="401">Authentication required</response>
    /// <response code="404">User not found</response>
    [HttpPut("{username}", Name = nameof(UpsertMyNoteAboutUser))]
    [ProducesResponseType(typeof(UserProfileNote), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertMyNoteAboutUser(string username, [FromBody] UserProfileNoteRequest request)
    {
        var note = await _userProfileNoteApiService.UpsertNote(username, request);
        if (note == null)
        {
            return NoContent();
        }
        return Ok(note);
    }

    /// <summary>
    /// Delete note about a user
    /// </summary>
    /// <remarks>
    /// Deletes your personal note about the specified user.
    /// Returns 204 even if no note existed.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <response code="204">Note deleted (or no note existed)</response>
    /// <response code="401">Authentication required</response>
    /// <response code="404">User not found</response>
    [HttpDelete("{username}", Name = nameof(DeleteMyNoteAboutUser))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMyNoteAboutUser(string username)
    {
        await _userProfileNoteApiService.DeleteNote(username);
        return NoContent();
    }
}
