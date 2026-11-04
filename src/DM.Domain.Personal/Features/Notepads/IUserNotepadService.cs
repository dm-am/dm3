using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Notepads;

namespace DM.Domain.Personal.Features.Notepads;

/// <summary>
/// Service for managing personal user notepad
/// </summary>
public interface IUserNotepadService
{
    /// <summary>
    /// Get personal notepad entries for current user
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetEntries(CancellationToken ct = default);

    /// <summary>
    /// Get entry by ID
    /// </summary>
    Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default);

    Task<NotepadEntry> CreateEntry(CreateNotepadEntry createEntry, CancellationToken ct = default);

    Task<NotepadEntry> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default);

    Task DeleteEntry(Guid entryId, CancellationToken ct = default);
}
