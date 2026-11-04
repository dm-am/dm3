using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Notepads;

namespace DM.Domain.Blog.Features.Notepads;

public interface IBlogNotepadService
{
    Task<IEnumerable<NotepadEntry>> GetEntries(Guid blogId, CancellationToken ct = default);

    Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default);

    Task<NotepadEntry> CreateEntry(Guid blogId, CreateNotepadEntry createEntry, CancellationToken ct = default);

    Task<NotepadEntry> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default);

    Task DeleteEntry(Guid entryId, CancellationToken ct = default);
}
