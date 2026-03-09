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
    /// Warning points (1-3)
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Whether the warning has been removed
    /// </summary>
    public bool IsRemoved { get; set; }
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
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
