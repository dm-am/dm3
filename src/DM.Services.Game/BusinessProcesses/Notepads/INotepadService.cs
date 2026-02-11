using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Game.BusinessProcesses.Notepads;

/// <summary>
/// Service for notepad management
/// </summary>
public interface INotepadService
{
    #region Entries

    /// <summary>
    /// Get notepad entries for a game (master notepad)
    /// </summary>
    Task<IEnumerable<NotepadEntryDto>> GetGameMasterEntries(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Get notepad entries for a player in a game
    /// </summary>
    Task<IEnumerable<NotepadEntryDto>> GetPlayerEntries(Guid gameId, Guid characterId, CancellationToken ct = default);

    /// <summary>
    /// Get personal user notepad entries
    /// </summary>
    Task<IEnumerable<NotepadEntryDto>> GetUserEntries(CancellationToken ct = default);

    /// <summary>
    /// Get entry by ID
    /// </summary>
    Task<NotepadEntryDto> GetEntry(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Create new entry
    /// </summary>
    Task<NotepadEntryDto> CreateEntry(CreateNotepadEntry createEntry, CancellationToken ct = default);

    /// <summary>
    /// Update entry
    /// </summary>
    Task<NotepadEntryDto> UpdateEntry(Guid entryId, UpdateNotepadEntry updateEntry, CancellationToken ct = default);

    /// <summary>
    /// Delete entry
    /// </summary>
    Task DeleteEntry(Guid entryId, CancellationToken ct = default);

    #endregion

    #region Categories

    /// <summary>
    /// Get categories for a notepad
    /// </summary>
    Task<IEnumerable<NotepadCategoryDto>> GetCategories(NotepadType notepadType, Guid containerId, Guid? ownerId = null, CancellationToken ct = default);

    /// <summary>
    /// Create new category
    /// </summary>
    Task<NotepadCategoryDto> CreateCategory(CreateNotepadCategory createCategory, CancellationToken ct = default);

    /// <summary>
    /// Update category
    /// </summary>
    Task<NotepadCategoryDto> UpdateCategory(Guid categoryId, UpdateNotepadCategory updateCategory, CancellationToken ct = default);

    /// <summary>
    /// Delete category
    /// </summary>
    Task DeleteCategory(Guid categoryId, CancellationToken ct = default);

    #endregion
}

#region DTOs

/// <summary>
/// Notepad entry DTO
/// </summary>
public class NotepadEntryDto
{
    /// <summary>Entry identifier</summary>
    public Guid Id { get; set; }

    /// <summary>Notepad type</summary>
    public NotepadType NotepadType { get; set; }

    /// <summary>Container ID (game/blog/user)</summary>
    public Guid ContainerId { get; set; }

    /// <summary>Owner ID (character for player notepad)</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>Category ID</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Entry title</summary>
    public string Title { get; set; } = null!;

    /// <summary>Entry content</summary>
    public string Content { get; set; } = null!;

    /// <summary>Sort order</summary>
    public int SortOrder { get; set; }

    /// <summary>Creation date</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Last update date</summary>
    public DateTimeOffset? UpdatedUtc { get; set; }
}

/// <summary>
/// Notepad category DTO
/// </summary>
public class NotepadCategoryDto
{
    /// <summary>Category identifier</summary>
    public Guid Id { get; set; }

    /// <summary>Notepad type</summary>
    public NotepadType NotepadType { get; set; }

    /// <summary>Container ID (game/blog/user)</summary>
    public Guid ContainerId { get; set; }

    /// <summary>Owner ID (character for player notepad)</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>Category name</summary>
    public string Name { get; set; } = null!;

    /// <summary>Sort order</summary>
    public int SortOrder { get; set; }

    /// <summary>Creation date</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Create notepad entry request
/// </summary>
public class CreateNotepadEntry
{
    /// <summary>Notepad type</summary>
    public NotepadType NotepadType { get; set; }

    /// <summary>Container ID (game/blog/user)</summary>
    public Guid ContainerId { get; set; }

    /// <summary>Owner ID (character for player notepad)</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>Category ID</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Entry title</summary>
    public string Title { get; set; } = null!;

    /// <summary>Entry content</summary>
    public string Content { get; set; } = null!;
}

/// <summary>
/// Update notepad entry request
/// </summary>
public class UpdateNotepadEntry
{
    /// <summary>Category ID</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Entry title</summary>
    public string? Title { get; set; }

    /// <summary>Entry content</summary>
    public string? Content { get; set; }

    /// <summary>Sort order</summary>
    public int? SortOrder { get; set; }
}

/// <summary>
/// Create notepad category request
/// </summary>
public class CreateNotepadCategory
{
    /// <summary>Notepad type</summary>
    public NotepadType NotepadType { get; set; }

    /// <summary>Container ID (game/blog/user)</summary>
    public Guid ContainerId { get; set; }

    /// <summary>Owner ID (character for player notepad)</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>Category name</summary>
    public string Name { get; set; } = null!;
}

/// <summary>
/// Update notepad category request
/// </summary>
public class UpdateNotepadCategory
{
    /// <summary>Category name</summary>
    public string? Name { get; set; }

    /// <summary>Sort order</summary>
    public int? SortOrder { get; set; }
}

#endregion
