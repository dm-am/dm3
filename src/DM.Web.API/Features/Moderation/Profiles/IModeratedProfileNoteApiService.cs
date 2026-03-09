using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// API service for moderator notes about users
/// </summary>
public interface IModeratedProfileNoteApiService
{
    /// <summary>
    /// Get all moderator notes for a user
    /// </summary>
    Task<IEnumerable<ModeratedProfileNote>> GetNotes(string username);

    /// <summary>
    /// Get a single moderator note
    /// </summary>
    Task<ModeratedProfileNote> GetNote(Guid noteId);

    /// <summary>
    /// Create a moderator note
    /// </summary>
    Task<ModeratedProfileNote> CreateNote(string username, CreateModeratedProfileNoteRequest request);

    /// <summary>
    /// Update a moderator note
    /// </summary>
    Task<ModeratedProfileNote> UpdateNote(Guid noteId, UpdateModeratedProfileNoteRequest request);

    /// <summary>
    /// Delete a moderator note
    /// </summary>
    Task DeleteNote(Guid noteId);
}
