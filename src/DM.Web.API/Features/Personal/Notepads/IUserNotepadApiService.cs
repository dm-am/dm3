using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Personal.Notepads;

/// <summary>
/// API service for user (personal) notepad management
/// </summary>
public interface IUserNotepadApiService
{
    /// <summary>
    /// Get personal user notepad entries
    /// </summary>
    Task<ListEnvelope<NotepadEntryResponse>> GetEntries();

    /// <summary>
    /// Get entry by ID
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> GetEntry(Guid entryId);

    /// <summary>
    /// Create entry in user notepad
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> CreateEntry(CreateNotepadEntryRequest request);

    /// <summary>
    /// Update entry
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> UpdateEntry(Guid entryId, UpdateNotepadEntryRequest request);

    /// <summary>
    /// Delete entry
    /// </summary>
    Task DeleteEntry(Guid entryId);
}
