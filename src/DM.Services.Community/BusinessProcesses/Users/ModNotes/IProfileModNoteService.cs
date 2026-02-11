using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Users.ModNotes;

/// <summary>
/// Service for moderator notes about users
/// </summary>
public interface IProfileModNoteService
{
    /// <summary>
    /// Get all moderator notes for a user
    /// </summary>
    /// <param name="userLogin">User login</param>
    /// <returns>List of notes</returns>
    Task<IEnumerable<ProfileModNoteDto>> GetNotes(string userLogin);

    /// <summary>
    /// Get a single moderator note by ID
    /// </summary>
    /// <param name="noteId">Note ID</param>
    /// <returns>Note</returns>
    Task<ProfileModNoteDto> GetNote(Guid noteId);

    /// <summary>
    /// Create a new moderator note
    /// </summary>
    /// <param name="createNote">Note data</param>
    /// <returns>Created note</returns>
    Task<ProfileModNoteDto> Create(CreateProfileModNote createNote);

    /// <summary>
    /// Update an existing moderator note
    /// </summary>
    /// <param name="updateNote">Note data</param>
    /// <returns>Updated note</returns>
    Task<ProfileModNoteDto> Update(UpdateProfileModNote updateNote);

    /// <summary>
    /// Delete a moderator note
    /// </summary>
    /// <param name="noteId">Note ID</param>
    /// <returns></returns>
    Task Delete(Guid noteId);
}
