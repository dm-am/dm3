using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Notepads;

namespace DM.Domain.Blog.Features.Notepads;

/// <summary>
/// Service for managing blog notepad
/// </summary>
public interface IBlogNotepadService
{
    #region Entries

    /// <summary>
    /// Get notepad entries for a blog
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetEntries(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get entry by ID
    /// </summary>
    Task<NotepadEntry> GetEntry(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Create new entry
    /// </summary>
    Task<NotepadEntry> CreateEntry(Guid blogId, CreateNotepadEntry createEntry, CancellationToken ct = default);

    /// <summary>
    /// Update entry
    /// </summary>
    Task<NotepadEntry> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default);

    /// <summary>
    /// Delete entry
    /// </summary>
    Task DeleteEntry(Guid entryId, CancellationToken ct = default);

    #endregion

    #region Categories

    /// <summary>
    /// Get categories for blog notepad
    /// </summary>
    Task<IEnumerable<NotepadCategory>> GetCategories(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Create new category
    /// </summary>
    Task<NotepadCategory> CreateCategory(Guid blogId, CreateNotepadCategory createCategory, CancellationToken ct = default);

    /// <summary>
    /// Update category
    /// </summary>
    Task<NotepadCategory> UpdateCategory(Guid categoryId, UpdateNotepadCategory updateCategory, CancellationToken ct = default);

    /// <summary>
    /// Delete category
    /// </summary>
    Task DeleteCategory(Guid categoryId, CancellationToken ct = default);

    #endregion
}
