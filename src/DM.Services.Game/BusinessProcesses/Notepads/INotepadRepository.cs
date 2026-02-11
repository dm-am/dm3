using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Notepads;

namespace DM.Services.Game.BusinessProcesses.Notepads;

/// <summary>
/// Repository for notepad data access
/// </summary>
public interface INotepadRepository
{
    #region Entries

    /// <summary>
    /// Get all entries in a notepad
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetEntries(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get entries in a specific category
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetEntriesByCategory(
        Guid categoryId,
        CancellationToken ct = default);

    /// <summary>
    /// Get entry by ID
    /// </summary>
    Task<NotepadEntry?> GetEntry(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Create new entry
    /// </summary>
    Task<NotepadEntry> CreateEntry(NotepadEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Update entry
    /// </summary>
    Task<NotepadEntry> UpdateEntry(NotepadEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Delete entry (soft delete)
    /// </summary>
    Task DeleteEntry(Guid entryId, Guid deletedByUserId, CancellationToken ct = default);

    #endregion

    #region Categories

    /// <summary>
    /// Get all categories in a notepad
    /// </summary>
    Task<IEnumerable<NotepadCategory>> GetCategories(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get category by ID
    /// </summary>
    Task<NotepadCategory?> GetCategory(Guid categoryId, CancellationToken ct = default);

    /// <summary>
    /// Create new category
    /// </summary>
    Task<NotepadCategory> CreateCategory(NotepadCategory category, CancellationToken ct = default);

    /// <summary>
    /// Update category
    /// </summary>
    Task<NotepadCategory> UpdateCategory(NotepadCategory category, CancellationToken ct = default);

    /// <summary>
    /// Delete category (soft delete)
    /// </summary>
    Task DeleteCategory(Guid categoryId, Guid deletedByUserId, CancellationToken ct = default);

    #endregion
}
