using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Notepads;

namespace DM.Web.API.Features.Blog.Notepads;

/// <summary>
/// API service for blog notepad management
/// </summary>
public interface IBlogNotepadApiService
{
    /// <summary>
    /// Get blog notepad entries
    /// </summary>
    Task<ListEnvelope<NotepadEntryResponse>> GetEntries(Guid blogId);

    /// <summary>
    /// Get entry by ID
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> GetEntry(Guid entryId);

    /// <summary>
    /// Create entry in blog notepad
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> CreateEntry(Guid blogId, CreateNotepadEntryRequest request);

    /// <summary>
    /// Update entry
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> UpdateEntry(Guid entryId, UpdateNotepadEntryRequest request);

    /// <summary>
    /// Delete entry
    /// </summary>
    Task DeleteEntry(Guid entryId);
}
