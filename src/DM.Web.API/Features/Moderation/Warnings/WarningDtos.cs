using System;
using System.Collections.Generic;
using DM.Domain.Moderation.Features.Warnings;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// What a warning carries in the moderation list and in the open log alike
/// </summary>
public abstract class WarningRecord
{
    /// <summary>
    /// Warning identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Kind of the offending content, a member of WarningEntityType: Comment, Topic,
    /// Message (of the global chat), GameReview, PostReview, Profile for a warning on
    /// the profile of the warned user. Null
    /// for a warning to the user with no place, the one for what was done where nothing
    /// is moderated.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Warning points as they stand now (0-6, 0 = the warning weighs nothing on
    /// the violation scale)
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Warning points as the moderator who issued the warning set them
    /// </summary>
    public int OriginalPoints { get; set; }

    /// <summary>
    /// Username of the moderator who last changed the points, null while nobody
    /// has changed them. Public along with the first signature: shown only the
    /// first, a reader reads a verdict that no longer stands.
    /// </summary>
    public string? PointsChangedBy { get; set; }

    /// <summary>
    /// When the points were last changed
    /// </summary>
    public DateTimeOffset? PointsChangedUtc { get; set; }

    /// <summary>
    /// Why the points were changed, in the words of whoever changed them
    /// </summary>
    public string? PointsChangeText { get; set; }

    /// <summary>
    /// Why the warning was issued, rendered: the moderator writes it in the
    /// markup of the site
    /// </summary>
    public CommonBbText Reason { get; set; } = null!;

    /// <summary>
    /// The offending text as it stood when the warning was issued, rendered by
    /// the rules of its place. Null when the warning names no content, and for a
    /// warning on a profile, whose words are in <see cref="Profile" />. In the
    /// open log absent as well where the reader does not read the place or none is found.
    /// </summary>
    /// <remarks>
    /// Written once, it survives an edit and a deletion of the content. Rendered,
    /// not the source: the source may carry a hidden block the page shows nobody.
    /// </remarks>
    public CommonBbText? Evidence { get; set; }

    /// <summary>
    /// The profile as it stood when the warning was issued, for a warning on a
    /// profile. Null for every other kind and for a removed account.
    /// </summary>
    public WarningProfile? Profile { get; set; }

    /// <summary>
    /// Site-relative address of the offending content. Null when the content is
    /// gone or has no page a moderator could open. In the open log present only
    /// where the reader could follow it: absent when the content or its place is
    /// gone, when moderation took the text off the site, and when the reader does
    /// not read the place.
    /// </summary>
    public string? EntityUrl { get; set; }

    /// <summary>
    /// Where the offending content stands, null when nothing says so and in the
    /// open log for a place the reader does not read
    /// </summary>
    public WarningPlaceKind? PlaceKind { get; set; }

    /// <summary>
    /// Title of that place: the topic, the game, the blog or the publication.
    /// Null for the global chat, which is named by its kind, and in the open log
    /// for a place the reader does not read.
    /// </summary>
    public string? PlaceTitle { get; set; }

    /// <summary>
    /// When the offending content was written. In the open log null for a place
    /// the reader does not read.
    /// </summary>
    public DateTimeOffset? EntityCreatedUtc { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Warning DTO
/// </summary>
public class Warning : WarningRecord
{
    /// <summary>
    /// User who received the warning
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Moderator who issued the warning
    /// </summary>
    public User Moderator { get; set; } = null!;

    /// <summary>
    /// Warning causation entity ID
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Whether this caller may change the points of this warning: a senior
    /// moderator may on any of them, a moderator on the ones he issued himself
    /// </summary>
    public bool CanChangePoints { get; set; }
}

/// <summary>
/// A row of the warning log everyone can read
/// </summary>
/// <remarks>
/// The row of its reader. The offending text is there edited or deleted
/// afterwards: it is still what the warning was given for. Where the reader does
/// not read the place by its own read rule, or no place is found, the text, the
/// place, its address and the date of the text are held back alike and the row
/// says <see cref="AccessRestricted" />, so a refusal reads as an absence.
/// </remarks>
public class WarningLogEntry : WarningRecord
{
    /// <summary>
    /// True where the reader does not read the place of the text, or none is
    /// found: the row stands for it with one block of restricted access. Absent
    /// otherwise.
    /// </summary>
    public bool? AccessRestricted { get; set; }

    /// <summary>
    /// User who received the warning
    /// </summary>
    public UserRef User { get; set; } = null!;

    /// <summary>
    /// Moderator who issued the warning
    /// </summary>
    public UserRef Moderator { get; set; } = null!;
}

/// <summary>
/// What a profile showed everybody when a warning was issued on it
/// </summary>
/// <remarks>
/// Taken at issue time and never rewritten, so the log keeps showing what the
/// warning was given for after the person changes his profile. The personal
/// fields of the profile (the name, the place, the contacts, the birthday) are
/// not part of it. Every part is absent when the profile had none of it.
/// </remarks>
public class WarningProfile
{
    /// <summary>
    /// The status
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// "О себе", rendered the way the profile renders it to a guest
    /// </summary>
    public InfoBbText? Info { get; set; }

    /// <summary>
    /// The avatar. Gone as soon as moderation deletes the picture.
    /// </summary>
    public UserPicture? Picture { get; set; }
}

/// <summary>
/// Request to create a warning
/// </summary>
public class CreateWarningRequest
{
    /// <summary>
    /// Target user username
    /// </summary>
    /// <example>problemuser</example>
    public string Username { get; set; } = "";

    /// <summary>
    /// Entity ID that caused the warning. Not read for a warning on a profile:
    /// its entity is the warned user.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// The place, a member of WarningEntityType by name: Comment, Topic, Message (of
    /// the global chat), GameReview, PostReview, or Profile for a warning on the
    /// profile of the warned user, which the server
    /// then copies (the avatar, the status and "О себе") as it stands at the moment of
    /// issue. Left out for a warning to the user with no place and no copy, the one
    /// for what was done in a game room or a conversation, which are not moderated. A
    /// name outside the list is refused.
    /// </summary>
    /// <example>Comment</example>
    public string? EntityType { get; set; }

    /// <summary>
    /// Warning points (0-6, 0 = verbal warning without points)
    /// </summary>
    /// <example>1</example>
    public int Points { get; set; } = 1;

    /// <summary>
    /// Warning reason/message
    /// </summary>
    /// <example>Rule violation: inappropriate content</example>
    public string Reason { get; set; } = "";
}

/// <summary>
/// Request to change the points of a warning
/// </summary>
/// <remarks>
/// The warning is not withdrawn by this and cannot be: only the points move. The
/// explanation is required and is shown wherever the warning is shown, the open
/// log included.
/// </remarks>
public class ChangeWarningPointsRequest
{
    /// <summary>
    /// New points (0-6, 0 = the warning stops weighing on the violation scale)
    /// </summary>
    /// <remarks>
    /// Nullable so that a body without the field is refused rather than read as
    /// zero: absence means "leave alone" in a patch, and zero is the one value
    /// that takes the warning off the scale entirely.
    /// </remarks>
    /// <example>0</example>
    public int? Points { get; set; }

    /// <summary>
    /// Why the points are being changed
    /// </summary>
    /// <example>Not a violation after all, the moderator was mistaken</example>
    public string Reason { get; set; } = "";
}

/// <summary>
/// Warning as visible to everyone on the public profile
/// </summary>
/// <remarks>
/// Aggregate facts only — reason, causation entity and moderator identity
/// stay in the moderation area (GET /v1/moderation/warnings).
/// </remarks>
public class PublicWarning
{
    /// <summary>
    /// Warning points as they stand now (0-6, 0 = the warning weighs nothing on
    /// the violation scale)
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// User warnings summary (public view)
/// </summary>
public class UserWarningsInfo
{
    /// <summary>
    /// User username
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Sum of the points the warnings weigh right now, corrections included
    /// </summary>
    public int TotalPoints { get; set; }

    /// <summary>
    /// How many warnings the profile carries, those corrected down to zero included
    /// </summary>
    public int ActiveCount { get; set; }

    /// <summary>
    /// List of warnings (trimmed public view)
    /// </summary>
    public IEnumerable<PublicWarning> Warnings { get; set; } = Array.Empty<PublicWarning>();
}
