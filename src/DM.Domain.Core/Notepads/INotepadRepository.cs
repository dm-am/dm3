using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Notepads;

/// <summary>
/// Shared repository for notepad data access (cross-module: Game, Blog, Personal)
/// </summary>
public interface INotepadRepository
{
    Task<IEnumerable<NotepadEntry>> GetEntriesAsync(
        NotepadType notepadType,
        Guid containerId,
        Guid? ownerId = null,
        CancellationToken ct = default);

    Task<NotepadEntry?> GetEntryAsync(Guid entryId, CancellationToken ct = default);

    Task<NotepadEntry> CreateEntryAsync(CreateNotepadEntryInternal entry, CancellationToken ct = default);

    Task<NotepadEntry> UpdateEntryAsync(UpdateNotepadEntryInternal entry, CancellationToken ct = default);

    /// <summary>
    /// Delete entry (soft delete)
    /// </summary>
    Task DeleteEntryAsync(Guid entryId, Guid deletedByUserId, CancellationToken ct = default);
}

/// <summary>
/// Internal DTO for creating notepad entry (repository level)
/// </summary>
public class CreateNotepadEntryInternal
{
    public Guid EntryId { get; set; }
    public NotepadType NotepadType { get; set; }
    public Guid ContainerId { get; set; }
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
    public string? Title { get; set; }
    /// <summary>Entry content</summary>
    public string? Content { get; set; }
    public int? SortOrder { get; set; }
    /// <summary>Update timestamp</summary>
    public DateTimeOffset ModifiedUtc { get; set; }
}
