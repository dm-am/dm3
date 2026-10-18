using System;
using System.Collections.Generic;
using DM.Domain.Moderation.Features.Warnings;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Warning DTO
/// </summary>
public class Warning
{
    /// <summary>
    /// Warning identifier
    /// </summary>
    public Guid Id { get; set; }

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
    /// Entity type (Comment, Message, Topic, or Profile for a warning on the
    /// profile of the warned user)
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
    /// has changed them
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
    /// Whether this caller may change the points of this warning: a senior
    /// moderator may on any of them, a moderator on the ones he issued himself
    /// </summary>
    public bool CanChangePoints { get; set; }

    /// <summary>
    /// Why the warning was issued, rendered: the moderator writes it in the
    /// markup of the site
    /// </summary>
    public CommonBbText Reason { get; set; } = null!;

    /// <summary>
    /// The offending text as it stood when the warning was issued, rendered the
    /// way its place renders it. Null when the warning names no content, and for
    /// a warning on a profile, whose words are in <see cref="Profile" />.
    /// </summary>
    /// <remarks>
    /// The copy is written once and never rewritten, so it survives both an edit
    /// and a deletion of the content it was taken from. It is rendered and not
    /// handed over as written: a moderator used to read the source, tags and all,
    /// and the source of a text may carry a hidden block the page never shows to
    /// anybody, moderators included.
    /// </remarks>
    public CommonBbText? Evidence { get; set; }

    /// <summary>
    /// The profile as it stood when the warning was issued, for a warning on a
    /// profile. Null for every other kind and for a removed account.
    /// </summary>
    public WarningProfile? Profile { get; set; }

    /// <summary>
    /// Site-relative address of the offending content. Null when the content is
    /// gone or has no page a moderator could open.
    /// </summary>
    public string? EntityUrl { get; set; }

    /// <summary>
    /// Where the offending content stands, null when nothing says so
    /// </summary>
    public WarningPlaceKind? PlaceKind { get; set; }

    /// <summary>
    /// Title of that place: the topic, the game, the blog or the publication.
    /// Null for the global chat, which is named by its kind.
    /// </summary>
    public string? PlaceTitle { get; set; }

    /// <summary>
    /// When the offending content was written
    /// </summary>
    public DateTimeOffset? EntityCreatedUtc { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// A row of the warning log everyone can read
/// </summary>
/// <remarks>
/// Carries who was warned, by whom, for how many points, why and when, and the
/// offending text with its place, as DM2's log did. The text is there always,
/// the owner's word for it (05.10: "Всегда показывай"): edited or deleted
/// afterwards, by its author or by moderation, it is still what the warning was
/// given for. It is rendered by the rules of its place, so a hidden block is
/// erased from it for everybody. The address alone is held back where a guest
/// could not follow it: the place is closed to him or gone, or the text was
/// taken off the site.
/// </remarks>
public class WarningLogEntry
{
    /// <summary>
    /// Warning identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User who received the warning
    /// </summary>
    public UserRef User { get; set; } = null!;

    /// <summary>
    /// Moderator who issued the warning
    /// </summary>
    public UserRef Moderator { get; set; } = null!;

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
    /// has changed them
    /// </summary>
    /// <remarks>
    /// The second signature is public along with the first: one moderator warned
    /// and wrote why, another took the points off because it was no violation,
    /// and a reader shown only the first sentence reads a verdict that no longer
    /// stands.
    /// </remarks>
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
    /// Kind of the offending content (Comment, Topic, Message), Profile for a
    /// warning on the profile of the warned user, null for a warning that names
    /// nothing (one issued on a ticket about a person)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// The offending text as it stood when the warning was issued, rendered by
    /// the rules of its place. Null when the warning names no content, and for a
    /// warning on a profile, whose words are in <see cref="Profile" />.
    /// </summary>
    public CommonBbText? Evidence { get; set; }

    /// <summary>
    /// The profile as it stood when the warning was issued, for a warning on a
    /// profile: what the profile showed everybody then, shown in the open log as
    /// the offending text is. Null for every other kind and for a removed
    /// account.
    /// </summary>
    public WarningProfile? Profile { get; set; }

    /// <summary>
    /// Where the offending content stands, null when nothing says so
    /// </summary>
    public WarningPlaceKind? PlaceKind { get; set; }

    /// <summary>
    /// Title of that place: the topic, the game, the blog or the publication.
    /// Null for the global chat, which is named by its kind.
    /// </summary>
    public string? PlaceTitle { get; set; }

    /// <summary>
    /// Site-relative address of the offending content. Present only where a
    /// guest could follow it: absent when the content or its place is gone,
    /// when moderation took the text off the site, and when the place is closed
    /// to a guest.
    /// </summary>
    public string? EntityUrl { get; set; }

    /// <summary>
    /// When the offending content was written
    /// </summary>
    public DateTimeOffset? EntityCreatedUtc { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
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
    /// Entity type: Comment, Message, Topic, or Profile for a warning on the
    /// profile of the warned user, which the server then copies (the avatar, the
    /// status and "О себе") as it stands at the moment of issue
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
    /// <remarks>
    /// It counted living warnings back when a lifted one disappeared from the
    /// list. Nothing disappears now, so the number is a count of the record, and
    /// the weight is <see cref="TotalPoints" />.
    /// </remarks>
    public int ActiveCount { get; set; }

    /// <summary>
    /// List of warnings (trimmed public view)
    /// </summary>
    public IEnumerable<PublicWarning> Warnings { get; set; } = Array.Empty<PublicWarning>();
}
