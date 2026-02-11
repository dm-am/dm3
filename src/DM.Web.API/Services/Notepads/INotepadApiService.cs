using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notepads;

namespace DM.Web.API.Services.Notepads;

/// <summary>
/// API service for notepad management
/// </summary>
public interface INotepadApiService
{
    /// <summary>
    /// Get master notepad entries for a game
    /// </summary>
    Task<ListEnvelope<NotepadEntry>> GetGameMasterEntries(Guid gameId);

    /// <summary>
    /// Get player notepad entries for a character
    /// </summary>
    Task<ListEnvelope<NotepadEntry>> GetPlayerEntries(Guid gameId, Guid characterId);

    /// <summary>
    /// Get player notepad entries for a character (derives gameId from character)
    /// </summary>
    Task<ListEnvelope<NotepadEntry>> GetCharacterNotepadEntries(Guid characterId);

    /// <summary>
    /// Get personal user notepad entries
    /// </summary>
    Task<ListEnvelope<NotepadEntry>> GetUserEntries();

    /// <summary>
    /// Get entry by ID
    /// </summary>
    Task<Envelope<NotepadEntry>> GetEntry(Guid entryId);

    /// <summary>
    /// Create entry in game master notepad
    /// </summary>
    Task<Envelope<NotepadEntry>> CreateGameMasterEntry(Guid gameId, CreateNotepadEntryRequest request);

    /// <summary>
    /// Create entry in player notepad
    /// </summary>
    Task<Envelope<NotepadEntry>> CreatePlayerEntry(Guid gameId, Guid characterId, CreateNotepadEntryRequest request);

    /// <summary>
    /// Create entry in character notepad (derives gameId from character)
    /// </summary>
    Task<Envelope<NotepadEntry>> CreateCharacterNotepadEntry(Guid characterId, CreateNotepadEntryRequest request);

    /// <summary>
    /// Create entry in user notepad
    /// </summary>
    Task<Envelope<NotepadEntry>> CreateUserEntry(CreateNotepadEntryRequest request);

    /// <summary>
    /// Update entry
    /// </summary>
    Task<Envelope<NotepadEntry>> UpdateEntry(Guid entryId, UpdateNotepadEntryRequest request);

    /// <summary>
    /// Delete entry
    /// </summary>
    Task DeleteEntry(Guid entryId);
}
