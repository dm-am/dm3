using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Output DTO for warning
/// </summary>
public class Warning
{
    /// <summary>
    /// Warning identifier
    /// </summary>
    public Guid WarningId { get; set; }

    /// <summary>
    /// Target user
    /// </summary>
    public GeneralUser TargetUser { get; set; } = null!;

    /// <summary>
    /// Warning author (moderator)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Entity ID that caused the warning
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Entity type that caused the warning
    /// </summary>
    public WarningEntityType EntityType { get; set; }

    /// <summary>
    /// Creation time
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Warning text/reason
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Warning points (0-6, 0 = verbal warning without points)
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Whether the warning has been removed
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Copy of the offending text, taken when the warning was issued
    /// </summary>
    public string? EntitySnapshot { get; set; }

    /// <summary>
    /// Site-relative address of the offending object, null when it cannot be
    /// addressed. Derived on read, never stored.
    /// </summary>
    public string? EntityUrl { get; set; }

    /// <summary>
    /// Whether the offending object was edited after the warning was issued, so
    /// the snapshot and what stands there now are not the same text. Derived on
    /// read from the edit history of the object's kind.
    /// </summary>
    public bool EntityEditedAfterWarning { get; set; }
}

/// <summary>
/// Entity DTO for creating a warning (repository level)
/// </summary>
public class CreateWarningEntity
{
    /// <summary>
    /// Warning identifier
    /// </summary>
    public Guid WarningId { get; set; }

    /// <summary>
    /// Target user identifier
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Entity ID that caused the warning
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Entity type
    /// </summary>
    public WarningEntityType EntityType { get; set; }

    /// <summary>
    /// Warning points
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Warning text
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Copy of the offending text as it stood when the warning was issued
    /// </summary>
    public string? EntitySnapshot { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
