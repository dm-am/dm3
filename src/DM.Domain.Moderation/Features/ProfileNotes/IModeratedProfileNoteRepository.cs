using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// Storage for moderator notes about users
/// </summary>
public interface IModeratedProfileNoteRepository
{
    /// <summary>
    /// Get all moderator notes for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of notes</returns>
    Task<IEnumerable<ModeratedProfileNote>> GetNotes(Guid userId);

    /// <summary>
    /// Get a single moderator note by ID
    /// </summary>
    /// <param name="noteId">Note ID</param>
    /// <returns>Note or null</returns>
    Task<ModeratedProfileNote?> GetNote(Guid noteId);

    /// <summary>
    /// Create a new moderator note
    /// </summary>
    /// <param name="entity">Note entity DTO</param>
    /// <returns>Created note</returns>
    Task<ModeratedProfileNote> Create(CreateModeratedProfileNoteEntity entity);

    /// <summary>
    /// Update an existing moderator note
    /// </summary>
    /// <param name="entity">Note update entity DTO</param>
    /// <returns></returns>
    Task Update(UpdateModeratedProfileNoteEntity entity);

    /// <summary>
    /// Soft-delete a moderator note
    /// </summary>
    /// <param name="noteId">Note ID</param>
    /// <returns></returns>
    Task Delete(Guid noteId);
}
