using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Personal.Features.ProfileNotes;

/// <summary>
/// Repository for user profile notes
/// </summary>
public interface IUserProfileNoteRepository
{
    /// <summary>
    /// Get note by owner and subject user
    /// </summary>
    Task<UserProfileNote?> Get(Guid ownerId, Guid subjectUserId, CancellationToken ct = default);

    /// <summary>
    /// Get note by ID
    /// </summary>
    Task<UserProfileNote?> GetById(Guid noteId, CancellationToken ct = default);

    /// <summary>
    /// Create a new note
    /// </summary>
    Task<UserProfileNote> Create(CreateUserProfileNoteEntity note, CancellationToken ct = default);

    /// <summary>
    /// Update an existing note
    /// </summary>
    Task<UserProfileNote> Update(UpdateUserProfileNoteEntity note, CancellationToken ct = default);

    /// <summary>
    /// Delete a note
    /// </summary>
    Task Delete(Guid noteId, CancellationToken ct = default);
}
