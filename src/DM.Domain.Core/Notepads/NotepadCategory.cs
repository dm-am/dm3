using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Notepads;

/// <summary>
/// Notepad category output DTO
/// </summary>
public class NotepadCategory
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
