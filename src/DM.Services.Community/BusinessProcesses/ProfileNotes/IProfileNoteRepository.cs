using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.ProfileNotes;

/// <summary>
/// Repository for profile notes
/// </summary>
public interface IProfileNoteRepository
{
    /// <summary>
    /// Get note by owner and subject user
    /// </summary>
    Task<ProfileNote?> Get(Guid ownerId, Guid subjectUserId, CancellationToken ct = default);

    /// <summary>
    /// Get note by ID
    /// </summary>
    Task<ProfileNote?> GetById(Guid noteId, CancellationToken ct = default);

    /// <summary>
    /// Create a new note
    /// </summary>
    Task<ProfileNote> Create(ProfileNote note, CancellationToken ct = default);

    /// <summary>
    /// Update an existing note
    /// </summary>
    Task<ProfileNote> Update(ProfileNote note, CancellationToken ct = default);

    /// <summary>
    /// Delete a note
    /// </summary>
    Task Delete(Guid noteId, CancellationToken ct = default);
}
