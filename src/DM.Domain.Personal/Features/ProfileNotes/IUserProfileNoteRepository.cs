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
    /// Write the owner's note about the subject, whether or not the pair already
    /// has one
    /// </summary>
    /// <remarks>
    /// A pair with no note gets a new one under <c>Id</c>. A pair that has one
    /// keeps it: the text is replaced and the modification dated with
    /// <c>CreatedUtc</c>, the identifier and creation date stay. Decided by the
    /// write and not by a read beforehand, so saves of one pair arriving together
    /// all land and the last one wins.
    ///
    /// Null when a removal of the same pair landed between the write and the
    /// read that answers it: the note is gone, and that is what the caller is
    /// told.
    /// </remarks>
    Task<UserProfileNote?> Create(CreateUserProfileNoteEntity note, CancellationToken ct = default);

    /// <summary>
    /// Delete a note; a note already gone is not an error
    /// </summary>
    Task Delete(Guid noteId, CancellationToken ct = default);
}
