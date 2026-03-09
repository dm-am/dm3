using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// Moderator notes about users
/// </summary>
/// <remarks>
/// Provides endpoints for managing private moderator notes about users.
/// These notes are only visible to moderators and are used for internal
/// moderation purposes (tracking problematic behavior, decisions, etc.)
///
/// ## Access Rules
/// - **Read/Create**: Moderator+
/// - **Edit/Delete own notes**: Any moderator
/// - **Edit/Delete others' notes**: SeniorModerator+
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("ProfileNotes")]
public class ProfileNoteController : ControllerBase
{
    private readonly IModeratedProfileNoteApiService _noteApiService;

    /// <inheritdoc />
    public ProfileNoteController(IModeratedProfileNoteApiService noteApiService)
    {
        _noteApiService = noteApiService;
    }

    /// <summary>
    /// Get moderator notes for a user
    /// </summary>
    /// <remarks>
    /// Returns all active moderator notes for the specified user.
    /// Notes are ordered by creation date (newest first).
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <response code="200">List of moderator notes</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    /// <response code="404">User not found</response>
    [HttpGet("users/{username}/notes", Name = nameof(GetUserModNotes))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<ModeratedProfileNote>), 200)]
    [ProducesResponseType(typeof(ErrorEnvelope), 401)]
    [ProducesResponseType(typeof(ErrorEnvelope), 403)]
    [ProducesResponseType(typeof(ErrorEnvelope), 404)]
    public async Task<IActionResult> GetUserModNotes(string username) =>
        Ok(await _noteApiService.GetNotes(username));

    /// <summary>
    /// Get a single moderator note
    /// </summary>
    /// <remarks>
    /// Returns a specific moderator note by ID.
    /// </remarks>
    /// <param name="id">Note identifier</param>
    /// <response code="200">Moderator note</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    /// <response code="404">Note not found</response>
    [HttpGet("notes/{id}", Name = nameof(GetModNote))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ModeratedProfileNote), 200)]
    [ProducesResponseType(typeof(ErrorEnvelope), 401)]
    [ProducesResponseType(typeof(ErrorEnvelope), 403)]
    [ProducesResponseType(typeof(ErrorEnvelope), 404)]
    public async Task<IActionResult> GetModNote(Guid id) =>
        Ok(await _noteApiService.GetNote(id));

    /// <summary>
    /// Create a moderator note
    /// </summary>
    /// <remarks>
    /// Creates a new moderator note about a user.
    /// The note author is automatically set to the current user.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <param name="request">Note content</param>
    /// <response code="201">Note created</response>
    /// <response code="400">Invalid note data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    /// <response code="404">User not found</response>
    [HttpPost("users/{username}/notes", Name = nameof(CreateModNote))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ModeratedProfileNote), 201)]
    [ProducesResponseType(typeof(ErrorEnvelope), 400)]
    [ProducesResponseType(typeof(ErrorEnvelope), 401)]
    [ProducesResponseType(typeof(ErrorEnvelope), 403)]
    [ProducesResponseType(typeof(ErrorEnvelope), 404)]
    public async Task<IActionResult> CreateModNote(string username, [FromBody] CreateModeratedProfileNoteRequest request)
    {
        var result = await _noteApiService.CreateNote(username, request);
        return CreatedAtRoute(nameof(GetModNote), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update a moderator note
    /// </summary>
    /// <remarks>
    /// Updates an existing moderator note.
    ///
    /// **Access Rules:**
    /// - Own notes: Any moderator
    /// - Others' notes: SeniorModerator+
    /// </remarks>
    /// <param name="id">Note identifier</param>
    /// <param name="request">Updated note content</param>
    /// <response code="200">Note updated</response>
    /// <response code="400">Invalid note data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Note not found</response>
    [HttpPut("notes/{id}", Name = nameof(UpdateModNote))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ModeratedProfileNote), 200)]
    [ProducesResponseType(typeof(ErrorEnvelope), 400)]
    [ProducesResponseType(typeof(ErrorEnvelope), 401)]
    [ProducesResponseType(typeof(ErrorEnvelope), 403)]
    [ProducesResponseType(typeof(ErrorEnvelope), 404)]
    public async Task<IActionResult> UpdateModNote(Guid id, [FromBody] UpdateModeratedProfileNoteRequest request) =>
        Ok(await _noteApiService.UpdateNote(id, request));

    /// <summary>
    /// Delete a moderator note
    /// </summary>
    /// <remarks>
    /// Soft-deletes a moderator note.
    ///
    /// **Access Rules:**
    /// - Own notes: Any moderator
    /// - Others' notes: SeniorModerator+
    /// </remarks>
    /// <param name="id">Note identifier</param>
    /// <response code="204">Note deleted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Note not found</response>
    [HttpDelete("notes/{id}", Name = nameof(DeleteModNote))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ErrorEnvelope), 401)]
    [ProducesResponseType(typeof(ErrorEnvelope), 403)]
    [ProducesResponseType(typeof(ErrorEnvelope), 404)]
    public async Task<IActionResult> DeleteModNote(Guid id)
    {
        await _noteApiService.DeleteNote(id);
        return NoContent();
    }
}
