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
    /// Warning points as they stand now (0-6, 0 = verbal warning without points)
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Warning points as the moderator who issued the warning set them
    /// </summary>
    public int OriginalPoints { get; set; }

    /// <summary>
    /// Name of the moderator who last changed the points, null while nobody has
    /// </summary>
    public string? PointsChangedByUsername { get; set; }

    /// <summary>
    /// When the points were last changed
    /// </summary>
    public DateTimeOffset? PointsChangedUtc { get; set; }

    /// <summary>
    /// Why the points were changed, written by whoever changed them
    /// </summary>
    public string? PointsChangeText { get; set; }

    /// <summary>
    /// Whether the reader of the moderation list may change the points of this
    /// warning. Derived per reader, never stored.
    /// </summary>
    public bool CanChangePoints { get; set; }

    /// <summary>
    /// Copy of the offending text, taken when the warning was issued. For a
    /// warning on a profile it is "О себе" of that profile.
    /// </summary>
    public string? EntitySnapshot { get; set; }

    /// <summary>
    /// The profile as it stood when the warning was issued, for a warning of
    /// the kind <see cref="WarningEntityType.Profile" /> and for no other
    /// </summary>
    public WarningProfile? Profile { get; set; }

    /// <summary>
    /// Site-relative address of the offending object, null when it cannot be
    /// addressed. Derived on read, never stored.
    /// </summary>
    public string? EntityUrl { get; set; }

    /// <summary>
    /// Where the offending object stands. Derived on read.
    /// </summary>
    public WarningPlaceKind? PlaceKind { get; set; }

    /// <summary>
    /// Title of that place: the topic, the game or the blog. Derived on read.
    /// </summary>
    public string? PlaceTitle { get; set; }

    /// <summary>
    /// When the offending object was written. Derived on read.
    /// </summary>
    public DateTimeOffset? EntityCreatedUtc { get; set; }

    /// <summary>
    /// Why a guest could not follow the link to the offending text, null when he
    /// could. Derived on read, from what a guest could read at that place right
    /// now; the text itself is shown in the open log whatever this says.
    /// </summary>
    public WarningLogClosure? LogClosure { get; set; }
}

/// <summary>
/// What a profile showed everybody when a warning was issued on it: the avatar,
/// the status and "О себе"
/// </summary>
/// <remarks>
/// The personal fields of the profile (the name, the place, the contacts, the
/// birthday) are not part of it, and neither is the login: they are the
/// person's to erase later, and the open log would keep them for good.
/// </remarks>
public class WarningProfile
{
    /// <summary>
    /// The status, null when the profile had none
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// "О себе" as written, null when the profile had none
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// The avatar the profile carried, null when it had none or when moderation
    /// has since deleted the picture
    /// </summary>
    public AvatarPicture? Picture { get; set; }
}

/// <summary>
/// The profile of the warned user as the warning takes it: read at issue time
/// and stored once
/// </summary>
public class ProfileCapture
{
    /// <summary>
    /// The status, null when there is none
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// "О себе", null when there is none
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// The upload the avatar is, null when there is no avatar
    /// </summary>
    public Guid? AvatarUploadId { get; set; }
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

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
