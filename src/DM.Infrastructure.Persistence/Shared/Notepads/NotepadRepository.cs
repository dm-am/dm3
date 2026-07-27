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
using NotepadCategoryEntity = DM.Infrastructure.Persistence.Entities.Personal.Notepads.NotepadCategory;

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
    public async Task<IEnumerable<NotepadEntry>> GetEntriesByCategoryAsync(
        Guid categoryId,
        CancellationToken ct = default)
    {
        var entries = await _dbContext.NotepadEntries
            .Where(e => !e.IsRemoved && e.CategoryId == categoryId)
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
            CategoryId = create.CategoryId,
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
        if (update.CategoryId.HasValue)
            entry.CategoryId = update.CategoryId;
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

    #region Categories

    /// <inheritdoc />
    public async Task<IEnumerable<NotepadCategory>> GetCategoriesAsync(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.NotepadCategories
            .Where(c => !c.IsRemoved && c.NotepadType == notepadType && c.ContainerId == containerId);

        if (ownerId.HasValue)
        {
            query = query.Where(c => c.OwnerId == ownerId.Value);
        }
        else
        {
            query = query.Where(c => c.OwnerId == null);
        }

        var categories = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        return categories.Select(MapCategoryToDto);
    }

    /// <inheritdoc />
    public async Task<NotepadCategory?> GetCategoryAsync(Guid categoryId, CancellationToken ct = default)
    {
        var category = await _dbContext.NotepadCategories
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId && !c.IsRemoved, ct);

        return category != null ? MapCategoryToDto(category) : null;
    }

    /// <inheritdoc />
    public async Task<NotepadCategory> CreateCategoryAsync(CreateNotepadCategoryInternal create, CancellationToken ct = default)
    {
        var category = new NotepadCategoryEntity
        {
            CategoryId = create.CategoryId,
            NotepadType = create.NotepadType,
            ContainerId = create.ContainerId,
            OwnerId = create.OwnerId,
            AuthorId = create.AuthorId,
            Name = create.Name,
            SortOrder = create.SortOrder,
            CreatedUtc = create.CreatedUtc,
            IsRemoved = false
        };

        _dbContext.NotepadCategories.Add(category);
        await _dbContext.SaveChangesAsync(ct);
        return MapCategoryToDto(category);
    }

    /// <inheritdoc />
    public async Task<NotepadCategory> UpdateCategoryAsync(UpdateNotepadCategoryInternal update, CancellationToken ct = default)
    {
        var category = await _dbContext.NotepadCategories.FindAsync(new object[] { update.CategoryId }, ct)
            ?? throw new InvalidOperationException($"Category {update.CategoryId} not found");

        if (update.Name != null)
            category.Name = update.Name;
        if (update.SortOrder.HasValue)
            category.SortOrder = update.SortOrder.Value;

        await _dbContext.SaveChangesAsync(ct);
        return MapCategoryToDto(category);
    }

    /// <inheritdoc />
    public async Task DeleteCategoryAsync(Guid categoryId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var category = await _dbContext.NotepadCategories.FindAsync(new object[] { categoryId }, ct);
        if (category != null)
        {
            category.IsRemoved = true;
            category.DeletedByUserId = deletedByUserId;
            category.DeletedUtc = _dateTimeProvider.Now;
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
        CategoryId = entry.CategoryId,
        Title = entry.Title,
        Content = entry.Content,
        SortOrder = entry.SortOrder,
        CreatedUtc = entry.CreatedUtc,
        ModifiedUtc = entry.ModifiedUtc
    };

    private static NotepadCategory MapCategoryToDto(NotepadCategoryEntity category) => new()
    {
        Id = category.CategoryId,
        NotepadType = category.NotepadType,
        ContainerId = category.ContainerId,
        OwnerId = category.OwnerId,
        Name = category.Name,
        SortOrder = category.SortOrder,
        CreatedUtc = category.CreatedUtc
    };

    #endregion
}
