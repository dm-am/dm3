using System;
using DM.Domain.Core.Authorization;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// What is currently true about the object a warning was issued for
/// </summary>
public class WarningEntityState
{
    /// <summary>
    /// Site-relative address of the object, null when it cannot be addressed
    /// (deleted, or a kind with no public page)
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Where the object stands, null when nothing says so
    /// </summary>
    public WarningPlaceKind? PlaceKind { get; set; }

    /// <summary>
    /// Title of the place: the topic, the game, the blog or the publication.
    /// Null for the global chat, which the reader names by its kind, and for an
    /// unknown place.
    /// </summary>
    public string? PlaceTitle { get; set; }

    /// <summary>
    /// When the object itself was written, null when it was not found
    /// </summary>
    public DateTimeOffset? CreatedUtc { get; set; }

    /// <summary>
    /// Why a guest could not follow the link to the object, null when he could.
    /// Read now and not at issue time: a place that closes later takes the link
    /// out of the log too.
    /// </summary>
    public WarningLogClosure? LogClosure { get; set; }

    /// <summary>
    /// The place a guest may not read (<see cref="WarningLogClosure.PlaceNotPublic" />),
    /// whose reading the open log asks of its viewer; null for every other closure
    /// </summary>
    public ReadablePlace? ClosedPlace { get; set; }

    /// <summary>
    /// The closure for a viewer who reads <see cref="ClosedPlace" />: what the
    /// place would answer were it open
    /// </summary>
    public WarningLogClosure? ClosureForReader { get; set; }
}
