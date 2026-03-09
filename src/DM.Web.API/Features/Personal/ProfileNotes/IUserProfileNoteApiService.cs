using System.Threading;
using System.Threading.Tasks;

namespace DM.Web.API.Features.Personal.ProfileNotes;

/// <summary>
/// API service for user profile notes
/// </summary>
public interface IUserProfileNoteApiService
{
    /// <summary>
    /// Get note for a specific user
    /// </summary>
    /// <param name="username">Target user's username</param>
    /// <param name="ct">Cancellation token</param>
    Task<UserProfileNote?> GetNote(string username, CancellationToken ct = default);

    /// <summary>
    /// Create or update a note for a user.
    /// If text is empty or whitespace, the note is deleted and null is returned.
    /// </summary>
    /// <param name="username">Target user's username</param>
    /// <param name="request">Note content</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated note or null if deleted</returns>
    Task<UserProfileNote?> UpsertNote(string username, UserProfileNoteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a note for a user
    /// </summary>
    /// <param name="username">Target user's username</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteNote(string username, CancellationToken ct = default);
}
