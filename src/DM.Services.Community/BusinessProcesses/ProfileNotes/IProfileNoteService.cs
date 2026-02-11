using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.ProfileNotes;

/// <summary>
/// Service for managing profile notes
/// </summary>
public interface IProfileNoteService
{
    /// <summary>
    /// Get note for a specific user (from current user's perspective)
    /// </summary>
    Task<ProfileNoteDto?> GetNote(string subjectUserLogin, CancellationToken ct = default);

    /// <summary>
    /// Create or update a note for a user
    /// </summary>
    Task<ProfileNoteDto> UpsertNote(CreateProfileNote createNote, CancellationToken ct = default);

    /// <summary>
    /// Delete a note
    /// </summary>
    Task DeleteNote(string subjectUserLogin, CancellationToken ct = default);
}
