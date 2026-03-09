using System;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Notepads;

// =====================
// Response DTOs
// =====================

/// <summary>
/// Notepad entry response
/// </summary>
public class NotepadEntryResponse
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
/// Notepad category response
/// </summary>
public class NotepadCategoryResponse
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

// =====================
// Request DTOs
// =====================

/// <summary>
/// Create notepad entry request
/// </summary>
public class CreateNotepadEntryRequest
{
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
public class UpdateNotepadEntryRequest
{
    /// <summary>Category ID</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Entry title</summary>
    public string Title { get; set; } = null!;

    /// <summary>Entry content</summary>
    public string Content { get; set; } = null!;

    /// <summary>Sort order</summary>
    public int? SortOrder { get; set; }
}

/// <summary>
/// Create notepad category request
/// </summary>
public class CreateNotepadCategoryRequest
{
    /// <summary>Category name</summary>
    public string Name { get; set; } = null!;
}

/// <summary>
/// Update notepad category request
/// </summary>
public class UpdateNotepadCategoryRequest
{
    /// <summary>Category name</summary>
    public string Name { get; set; } = null!;

    /// <summary>Sort order</summary>
    public int? SortOrder { get; set; }
}
