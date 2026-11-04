using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Entity DTO for creating a warning (repository level)
/// </summary>
public class CreateWarningEntity
{
    public Guid WarningId { get; set; }

    public Guid TargetUserId { get; set; }

    public Guid AuthorId { get; set; }

    public Guid EntityId { get; set; }

    public WarningEntityType EntityType { get; set; }

    public int Points { get; set; }

    public string Text { get; set; } = "";

    /// <summary>
    /// Copy of the offending text as it stood when the warning was issued
    /// </summary>
    public string? EntitySnapshot { get; set; }

    /// <summary>
    /// The status of the warned profile when the warning was issued, for a
    /// warning on a profile
    /// </summary>
    public string? ProfileStatusSnapshot { get; set; }

    /// <summary>
    /// The upload the avatar of the warned profile was when the warning was
    /// issued, for a warning on a profile. The warning keeps the picture for as
    /// long as moderation does not delete it.
    /// </summary>
    public Guid? ProfileAvatarUploadId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}
