using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Notepads;

namespace DM.Domain.Game.Features.Notepads;

/// <summary>
/// The notepad of the game ("Заметки игры"), a character's own ("Заметки игрока") and the leads'
/// notes about a character ("Заметки мастера")
/// </summary>
public interface IGameNotepadService
{
    Task<IEnumerable<NotepadEntry>> GetMasterEntries(Guid gameId, CancellationToken ct = default);

    Task<NotepadEntry> CreateMasterEntry(Guid gameId, CreateNotepadEntry createEntry, CancellationToken ct = default);

    /// <summary>
    /// The owner of the character and nobody else, the leads included
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetPlayerEntries(Guid gameId, Guid characterId, CancellationToken ct = default);

    Task<NotepadEntry> CreatePlayerEntry(Guid gameId, Guid characterId, CreateNotepadEntry createEntry, CancellationToken ct = default);

    /// <summary>
    /// Every character has one, not only an NPC
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetCharacterMasterEntries(Guid gameId, Guid characterId, CancellationToken ct = default);

    Task<NotepadEntry> CreateCharacterMasterEntry(Guid gameId, Guid characterId, CreateNotepadEntry createEntry, CancellationToken ct = default);

    Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default);

    Task<NotepadEntry> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default);

    Task DeleteEntry(Guid entryId, CancellationToken ct = default);
}
