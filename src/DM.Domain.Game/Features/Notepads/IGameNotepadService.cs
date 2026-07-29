using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Notepads;

namespace DM.Domain.Game.Features.Notepads;

/// <summary>
/// Service for managing game notepads (master and player/character)
/// </summary>
public interface IGameNotepadService
{
    #region Master Notepad

    /// <summary>
    /// Get master notepad entries for a game
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetMasterEntries(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Create entry in master notepad
    /// </summary>
    Task<NotepadEntry> CreateMasterEntry(Guid gameId, CreateNotepadEntry createEntry, CancellationToken ct = default);

    #endregion

    #region Player/Character Notepad

    /// <summary>
    /// Get player notepad entries for a character
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetPlayerEntries(Guid gameId, Guid characterId, CancellationToken ct = default);

    /// <summary>
    /// Create entry in player notepad
    /// </summary>
    Task<NotepadEntry> CreatePlayerEntry(Guid gameId, Guid characterId, CreateNotepadEntry createEntry, CancellationToken ct = default);

    #endregion

    #region Common Operations

    /// <summary>
    /// Get entry by ID
    /// </summary>
    Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Update entry
    /// </summary>
    Task<NotepadEntry> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default);

    /// <summary>
    /// Delete entry
    /// </summary>
    Task DeleteEntry(Guid entryId, CancellationToken ct = default);

    #endregion
}
