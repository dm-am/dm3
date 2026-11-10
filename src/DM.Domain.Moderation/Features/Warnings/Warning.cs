using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

public class Warning
{
    public Guid WarningId { get; set; }

    public GeneralUser TargetUser { get; set; } = null!;

    /// <summary>
    /// The moderator who issued the warning, null for a removed account
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Entity ID that caused the warning
    /// </summary>
    public Guid EntityId { get; set; }

    public WarningEntityType EntityType { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Why the warning was issued
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Warning points as they stand now, 0 being a verbal warning
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
    /// now.
    /// </summary>
    public WarningLogClosure? LogClosure { get; set; }

    /// <summary>
    /// The place a guest may not read, whose reading the open log asks of its
    /// viewer. Derived on read.
    /// </summary>
    public ReadablePlace? ClosedPlace { get; set; }

    /// <summary>
    /// The closure for a viewer who reads <see cref="ClosedPlace" />. Derived on read.
    /// </summary>
    public WarningLogClosure? ClosureForReader { get; set; }

    /// <summary>
    /// Whether the open log gives the copy of the text and the title of its place.
    /// </summary>
    /// <remarks>
    /// Not where the place is closed to a guest, and not where nothing tells where
    /// the text stood, so whether a guest could read it there has no answer: the
    /// log is read by everybody, and a closed place is read by its own people alone.
    /// A text gone from an open place is still shown, as the one the warning was
    /// given for.
    /// </remarks>
    public bool LogShowsText => LogClosure is not (
        WarningLogClosure.NotFound or WarningLogClosure.PlaceNotPublic);
}
