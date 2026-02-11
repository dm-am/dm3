using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Moderation;

namespace DM.Web.API.Services.Moderation;

/// <summary>
/// API service for moderator notes about users
/// </summary>
public interface IProfileModNoteApiService
{
    /// <summary>
    /// Get all moderator notes for a user
    /// </summary>
    Task<ListEnvelope<ProfileModNote>> GetNotes(string login);

    /// <summary>
    /// Get a single moderator note
    /// </summary>
    Task<Envelope<ProfileModNote>> GetNote(Guid noteId);

    /// <summary>
    /// Create a moderator note
    /// </summary>
    Task<Envelope<ProfileModNote>> CreateNote(string login, CreateProfileModNoteRequest request);

    /// <summary>
    /// Update a moderator note
    /// </summary>
    Task<Envelope<ProfileModNote>> UpdateNote(Guid noteId, UpdateProfileModNoteRequest request);

    /// <summary>
    /// Delete a moderator note
    /// </summary>
    Task DeleteNote(Guid noteId);
}
