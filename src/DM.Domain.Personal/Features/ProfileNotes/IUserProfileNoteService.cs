using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Personal.Features.ProfileNotes;

/// <summary>
/// Service for managing user profile notes
/// </summary>
public interface IUserProfileNoteService
{
    /// <summary>
    /// Get note for a specific user (from current user's perspective)
    /// </summary>
    Task<UserProfileNote?> GetNote(string subjectUsername, CancellationToken ct = default);

    /// <summary>
    /// Create or update a note for a user.
    /// If text is empty, the note is deleted and returns null.
    /// </summary>
    /// <remarks>
    /// Also null when a removal of the same note arriving together with the save
    /// landed after it: the note is gone, and the answer says so.
    /// </remarks>
    Task<UserProfileNote?> UpsertNote(CreateUserProfileNote createNote, CancellationToken ct = default);

    /// <summary>
    /// Delete a note
    /// </summary>
    Task DeleteNote(string subjectUsername, CancellationToken ct = default);
}
