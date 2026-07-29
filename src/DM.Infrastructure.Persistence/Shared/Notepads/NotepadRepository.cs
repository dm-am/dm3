using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Notepads;
using Microsoft.EntityFrameworkCore;
using NotepadEntryEntity = DM.Infrastructure.Persistence.Entities.Personal.Notepads.NotepadEntry;

namespace DM.Infrastructure.Persistence.Shared.Notepads;

/// <inheritdoc />
internal class NotepadRepository : INotepadRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public NotepadRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    #region Entries

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntry>> GetEntriesAsync(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.NotepadEntries
            .Where(e => !e.IsRemoved && e.NotepadType == notepadType && e.ContainerId == containerId);

        if (ownerId.HasValue)
        {
            query = query.Where(e => e.OwnerId == ownerId.Value);
        }
        else
        {
            query = query.Where(e => e.OwnerId == null);
        }

        var entries = await query
            .OrderBy(e => e.SortOrder)
            .ThenByDescending(e => e.CreatedUtc)
            .ToListAsync(ct);

        return entries.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry?> GetEntryAsync(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _dbContext.NotepadEntries
            .FirstOrDefaultAsync(e => e.EntryId == entryId && !e.IsRemoved, ct);

        return entry != null ? MapToDto(entry) : null;
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> CreateEntryAsync(CreateNotepadEntryInternal create, CancellationToken ct = default)
    {
        var entry = new NotepadEntryEntity
        {
            EntryId = create.EntryId,
            NotepadType = create.NotepadType,
            ContainerId = create.ContainerId,
            OwnerId = create.OwnerId,
            AuthorId = create.AuthorId,
            Title = create.Title,
            Content = create.Content,
            SortOrder = create.SortOrder,
            CreatedUtc = create.CreatedUtc,
            IsRemoved = false
        };

        _dbContext.NotepadEntries.Add(entry);
        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(entry);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> UpdateEntryAsync(UpdateNotepadEntryInternal update, CancellationToken ct = default)
    {
        var entry = await _dbContext.NotepadEntries.FindAsync(new object[] { update.EntryId }, ct)
            ?? throw new InvalidOperationException($"Entry {update.EntryId} not found");

        if (update.Title != null)
            entry.Title = update.Title;
        if (update.Content != null)
            entry.Content = update.Content;
        if (update.SortOrder.HasValue)
            entry.SortOrder = update.SortOrder.Value;

        entry.ModifiedUtc = update.ModifiedUtc;

        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(entry);
    }

    /// <inheritdoc />
    public async Task DeleteEntryAsync(Guid entryId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var entry = await _dbContext.NotepadEntries.FindAsync(new object[] { entryId }, ct);
        if (entry != null)
        {
            entry.IsRemoved = true;
            entry.DeletedByUserId = deletedByUserId;
            entry.DeletedUtc = _dateTimeProvider.Now;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    #endregion

    #region Mapping

    private static NotepadEntry MapToDto(NotepadEntryEntity entry) => new()
    {
        Id = entry.EntryId,
        NotepadType = entry.NotepadType,
        ContainerId = entry.ContainerId,
        OwnerId = entry.OwnerId,
        Title = entry.Title,
        Content = entry.Content,
        SortOrder = entry.SortOrder,
        CreatedUtc = entry.CreatedUtc,
        ModifiedUtc = entry.ModifiedUtc
    };

    #endregion
}
