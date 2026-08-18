using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Notepads;

namespace DM.Domain.Game.Features.Notepads;

/// <summary>
/// Service for the three notepads of a game: the notepad of the game itself
/// ("Заметки игры"), a character's own notes ("Заметки игрока") and the notes
/// its leads keep about that character ("Заметки мастера")
/// </summary>
public interface IGameNotepadService
{
    #region Master Notepad

    /// <summary>
    /// Get the entries of the game's own notepad. Master and assistants
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetMasterEntries(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Create entry in the game's own notepad
    /// </summary>
    Task<NotepadEntry> CreateMasterEntry(Guid gameId, CreateNotepadEntry createEntry, CancellationToken ct = default);

    #endregion

    #region Player/Character Notepad

    /// <summary>
    /// Get a character's own notepad entries. The owner of the character and
    /// nobody else, the game leads included
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetPlayerEntries(Guid gameId, Guid characterId, CancellationToken ct = default);

    /// <summary>
    /// Create entry in a character's own notepad
    /// </summary>
    Task<NotepadEntry> CreatePlayerEntry(Guid gameId, Guid characterId, CreateNotepadEntry createEntry, CancellationToken ct = default);

    #endregion

    #region Character Master Notepad

    /// <summary>
    /// Get the notes the game leads keep about a character. Master and
    /// assistants; every character has such a notepad, not only an NPC
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetCharacterMasterEntries(Guid gameId, Guid characterId, CancellationToken ct = default);

    /// <summary>
    /// Create entry in the notes the game leads keep about a character
    /// </summary>
    Task<NotepadEntry> CreateCharacterMasterEntry(Guid gameId, Guid characterId, CreateNotepadEntry createEntry, CancellationToken ct = default);

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
