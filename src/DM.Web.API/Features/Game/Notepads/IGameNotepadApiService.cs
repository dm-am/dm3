using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Notepads;

namespace DM.Web.API.Features.Game.Notepads;

/// <summary>
/// API service for game notepad management (master and player)
/// </summary>
public interface IGameNotepadApiService
{
    #region Master Notepad

    /// <summary>
    /// Get master notepad entries for a game
    /// </summary>
    Task<ListEnvelope<NotepadEntryResponse>> GetMasterEntries(Guid gameId);

    /// <summary>
    /// Create entry in master notepad
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> CreateMasterEntry(Guid gameId, CreateNotepadEntryRequest request);

    #endregion

    #region Player/Character Notepad

    /// <summary>
    /// Get player notepad entries for a character
    /// </summary>
    Task<ListEnvelope<NotepadEntryResponse>> GetPlayerEntries(Guid gameId, Guid characterId);

    /// <summary>
    /// Get player notepad entries by character ID (derives gameId from character)
    /// </summary>
    Task<ListEnvelope<NotepadEntryResponse>> GetCharacterNotepadEntries(Guid characterId);

    /// <summary>
    /// Create entry in player notepad
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> CreatePlayerEntry(Guid gameId, Guid characterId, CreateNotepadEntryRequest request);

    /// <summary>
    /// Create entry in character notepad (derives gameId from character)
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> CreateCharacterNotepadEntry(Guid characterId, CreateNotepadEntryRequest request);

    #endregion

    #region Common Operations

    /// <summary>
    /// Get entry by ID
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> GetEntry(Guid entryId);

    /// <summary>
    /// Update entry
    /// </summary>
    Task<Envelope<NotepadEntryResponse>> UpdateEntry(Guid entryId, UpdateNotepadEntryRequest request);

    /// <summary>
    /// Delete entry
    /// </summary>
    Task DeleteEntry(Guid entryId);

    #endregion
}
