using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// Service for moderator notes about users
/// </summary>
public interface IModeratedProfileNoteService
{
    /// <summary>
    /// Get all moderator notes for a user
    /// </summary>
    /// <param name="username">Username</param>
    /// <returns>List of notes</returns>
    Task<IEnumerable<ModeratedProfileNote>> GetNotes(string username);

    /// <summary>
    /// Get a single moderator note by ID
    /// </summary>
    /// <param name="noteId">Note ID</param>
    /// <returns>Note</returns>
    Task<ModeratedProfileNote> GetNote(Guid noteId);

    /// <summary>
    /// Create a new moderator note
    /// </summary>
    /// <param name="createNote">Note data</param>
    /// <returns>Created note</returns>
    Task<ModeratedProfileNote> Create(CreateModeratedProfileNote createNote);

    /// <summary>
    /// Update an existing moderator note
    /// </summary>
    /// <param name="updateNote">Note data</param>
    /// <returns>Updated note</returns>
    Task<ModeratedProfileNote> Update(UpdateModeratedProfileNote updateNote);

    /// <summary>
    /// Delete a moderator note
    /// </summary>
    /// <param name="noteId">Note ID</param>
    /// <returns></returns>
    Task Delete(Guid noteId);
}
