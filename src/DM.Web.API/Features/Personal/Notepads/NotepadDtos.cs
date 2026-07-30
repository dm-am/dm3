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

    /// <summary>Entry title</summary>
    public string Title { get; set; } = null!;

    /// <summary>Entry content</summary>
    public string Content { get; set; } = null!;

    /// <summary>Sort order</summary>
    public int SortOrder { get; set; }

    /// <summary>Creation date</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Last update date</summary>
    public DateTimeOffset? ModifiedUtc { get; set; }
}

// =====================
// Request DTOs
// =====================

/// <summary>
/// Create notepad entry request
/// </summary>
public class CreateNotepadEntryRequest
{
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
    /// <summary>Entry title. Omit to leave unchanged.</summary>
    /// <remarks>
    /// Nullable because this is a PATCH: every layer below already treats null
    /// as "leave alone", and only this DTO declared the fields required, so
    /// changing a title alone answered 400 "The Content field is required".
    /// </remarks>
    public string? Title { get; set; }

    /// <summary>Entry content. Omit to leave unchanged.</summary>
    public string? Content { get; set; }

    /// <summary>Sort order. Omit to leave unchanged.</summary>
    public int? SortOrder { get; set; }
}
