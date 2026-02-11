using System.Threading;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <summary>
/// API service for profile notes
/// </summary>
public interface IProfileNoteApiService
{
    /// <summary>
    /// Get note for a specific user
    /// </summary>
    Task<Envelope<ProfileNote>?> GetNote(string login, CancellationToken ct = default);

    /// <summary>
    /// Create or update a note for a user
    /// </summary>
    Task<Envelope<ProfileNote>> UpsertNote(string login, ProfileNoteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a note for a user
    /// </summary>
    Task DeleteNote(string login, CancellationToken ct = default);
}
