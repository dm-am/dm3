using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// DTO model for notification creating
/// </summary>
public record CreateNotification
{
    /// <summary>
    /// Interested user identifiers
    /// </summary>
    public IEnumerable<Guid> UsersInterested { get; set; } = [];

    /// <summary>
    /// Event type
    /// </summary>
    public EventType EventType { get; set; }

    /// <summary>
    /// Event metadata
    /// </summary>
    public object Metadata { get; set; } = null!;

    /// <summary>
    /// Who did the thing being reported, when a person did it
    /// </summary>
    /// <remarks>
    /// Read by <see cref="INotificationService.CreateAsync"/> to drop the
    /// recipients who have this person on their personal blacklist. Blocking
    /// somebody means not hearing about them, and before this field the
    /// notification carried the actor only inside <see cref="Metadata"/>, as a
    /// display name in an anonymous object nothing could filter on.
    ///
    /// Null on purpose in two cases, and the difference matters:
    /// a system event has no actor at all (a deadline, a reminder, a change of
    /// one's own password), and a moderation event has one who must not be
    /// filterable — a warning is not less delivered because its author was
    /// blocked. Both stay null, and the generator says which of the two it is.
    /// </remarks>
    public Guid? ActorId { get; set; }

    /// <summary>
    /// Delivery is realtime only: push it to the recipients over the hub, store
    /// nothing, send neither email nor bot message
    /// </summary>
    /// <remarks>
    /// For an event whose whole job is to make an open tab re-read a counter it
    /// already shows. Storing one would put a row in the notification list and
    /// mail it out, and what to tell the user is a decision of its own, not a
    /// side effect of refreshing a badge.
    /// </remarks>
    public bool RealtimeOnly { get; set; }
}