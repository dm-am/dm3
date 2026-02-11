using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Notepads;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Game.BusinessProcesses.Notepads;

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
    public async Task<IEnumerable<NotepadEntry>> GetEntries(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.NotepadEntries
            .Where(e => e.NotepadType == notepadType && e.ContainerId == containerId);

        if (ownerId.HasValue)
        {
            query = query.Where(e => e.OwnerId == ownerId.Value);
        }
        else
        {
            query = query.Where(e => e.OwnerId == null);
        }

        return await query
            .OrderBy(e => e.SortOrder)
            .ThenByDescending(e => e.CreatedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadEntry>> GetEntriesByCategory(
        Guid categoryId,
        CancellationToken ct = default)
    {
        return await _dbContext.NotepadEntries
            .Where(e => !e.IsRemoved && e.CategoryId == categoryId)
            .OrderBy(e => e.SortOrder)
            .ThenByDescending(e => e.CreatedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry?> GetEntry(Guid entryId, CancellationToken ct = default)
    {
        return await _dbContext.NotepadEntries
            .FirstOrDefaultAsync(e => e.EntryId == entryId && !e.IsRemoved, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> CreateEntry(NotepadEntry entry, CancellationToken ct = default)
    {
        _dbContext.NotepadEntries.Add(entry);
        await _dbContext.SaveChangesAsync(ct);
        return entry;
    }

    /// <inheritdoc />
    public async Task<NotepadEntry> UpdateEntry(NotepadEntry entry, CancellationToken ct = default)
    {
        entry.UpdatedUtc = _dateTimeProvider.Now;
        _dbContext.NotepadEntries.Update(entry);
        await _dbContext.SaveChangesAsync(ct);
        return entry;
    }

    /// <inheritdoc />
    public async Task DeleteEntry(Guid entryId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var entry = await _dbContext.NotepadEntries.FindAsync(new object[] { entryId }, ct);
        if (entry != null)
        {
            entry.IsRemoved = true;
            entry.DeletedByUserId = deletedByUserId;
            entry.DeletedAtUtc = _dateTimeProvider.Now;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    #endregion

    #region Categories

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadCategory>> GetCategories(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.NotepadCategories
            .Where(c => c.NotepadType == notepadType && c.ContainerId == containerId);

        if (ownerId.HasValue)
        {
            query = query.Where(c => c.OwnerId == ownerId.Value);
        }
        else
        {
            query = query.Where(c => c.OwnerId == null);
        }

        return await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<NotepadCategory?> GetCategory(Guid categoryId, CancellationToken ct = default)
    {
        return await _dbContext.NotepadCategories
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId && !c.IsRemoved, ct);
    }

    /// <inheritdoc />
    public async Task<NotepadCategory> CreateCategory(NotepadCategory category, CancellationToken ct = default)
    {
        _dbContext.NotepadCategories.Add(category);
        await _dbContext.SaveChangesAsync(ct);
        return category;
    }

    /// <inheritdoc />
    public async Task<NotepadCategory> UpdateCategory(NotepadCategory category, CancellationToken ct = default)
    {
        _dbContext.NotepadCategories.Update(category);
        await _dbContext.SaveChangesAsync(ct);
        return category;
    }

    /// <inheritdoc />
    public async Task DeleteCategory(Guid categoryId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var category = await _dbContext.NotepadCategories.FindAsync(new object[] { categoryId }, ct);
        if (category != null)
        {
            category.IsRemoved = true;
            category.DeletedByUserId = deletedByUserId;
            category.DeletedAtUtc = _dateTimeProvider.Now;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    #endregion
}
