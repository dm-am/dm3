using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Users.ModNotes;

/// <summary>
/// Storage for moderator notes about users
/// </summary>
internal interface IProfileModNoteRepository
{
    /// <summary>
    /// Get all moderator notes for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of notes</returns>
    Task<IEnumerable<ProfileModNote>> GetNotes(Guid userId);

    /// <summary>
    /// Get a single moderator note by ID
    /// </summary>
    /// <param name="noteId">Note ID</param>
    /// <returns>Note or null</returns>
    Task<ProfileModNote?> GetNote(Guid noteId);

    /// <summary>
    /// Create a new moderator note
    /// </summary>
    /// <param name="note">Note entity</param>
    /// <returns>Created note</returns>
    Task<ProfileModNote> Create(ProfileModNote note);

    /// <summary>
    /// Update an existing moderator note
    /// </summary>
    /// <param name="note">Note entity</param>
    /// <returns></returns>
    Task Update(ProfileModNote note);

    /// <summary>
    /// Soft-delete a moderator note
    /// </summary>
    /// <param name="noteId">Note ID</param>
    /// <returns></returns>
    Task Delete(Guid noteId);
}
