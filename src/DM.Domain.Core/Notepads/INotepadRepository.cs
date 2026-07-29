using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Notepads;

/// <summary>
/// Shared repository for notepad data access (cross-module: Game, Blog, Personal)
/// </summary>
public interface INotepadRepository
{
    /// <summary>
    /// Get all entries in a notepad
    /// </summary>
    Task<IEnumerable<NotepadEntry>> GetEntriesAsync(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get entry by ID
    /// </summary>
    Task<NotepadEntry?> GetEntryAsync(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Create new entry
    /// </summary>
    Task<NotepadEntry> CreateEntryAsync(CreateNotepadEntryInternal entry, CancellationToken ct = default);

    /// <summary>
    /// Update entry
    /// </summary>
    Task<NotepadEntry> UpdateEntryAsync(UpdateNotepadEntryInternal entry, CancellationToken ct = default);

    /// <summary>
    /// Delete entry (soft delete)
    /// </summary>
    Task DeleteEntryAsync(Guid entryId, Guid deletedByUserId, CancellationToken ct = default);
}

#region Internal DTOs for repository

/// <summary>
/// Internal DTO for creating notepad entry (repository level)
/// </summary>
public class CreateNotepadEntryInternal
{
    /// <summary>Entry identifier</summary>
    public Guid EntryId { get; set; }
    /// <summary>Notepad type</summary>
    public NotepadType NotepadType { get; set; }
    /// <summary>Container identifier</summary>
    public Guid ContainerId { get; set; }
    /// <summary>Owner identifier</summary>
    public Guid? OwnerId { get; set; }
    /// <summary>Author identifier</summary>
    public Guid AuthorId { get; set; }
    /// <summary>Entry title</summary>
    public string Title { get; set; } = null!;
    /// <summary>Entry content</summary>
    public string Content { get; set; } = null!;
    /// <summary>Sort order</summary>
    public int SortOrder { get; set; }
    /// <summary>Creation timestamp</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Internal DTO for updating notepad entry (repository level)
/// </summary>
public class UpdateNotepadEntryInternal
{
    /// <summary>Entry identifier</summary>
    public Guid EntryId { get; set; }
    /// <summary>Entry title</summary>
    public string? Title { get; set; }
    /// <summary>Entry content</summary>
    public string? Content { get; set; }
    /// <summary>Sort order</summary>
    public int? SortOrder { get; set; }
    /// <summary>Update timestamp</summary>
    public DateTimeOffset ModifiedUtc { get; set; }
}

#endregion
