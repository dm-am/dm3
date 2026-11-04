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

    public EventType EventType { get; set; }

    /// <summary>
    /// Event metadata
    /// </summary>
    public object Metadata { get; set; } = null!;

    /// <summary>
    /// Identity of the bus publication this notification answers, when it has one
    /// </summary>
    /// <remarks>
    /// The key <see cref="INotificationService.CreateAsync"/> deduplicates by; null
    /// (the dispatcher maps <see cref="Guid.Empty"/> here) deduplicates nothing.
    /// </remarks>
    public Guid? EventId { get; set; }

    /// <summary>
    /// Who did the thing being reported, when a person did it
    /// </summary>
    /// <remarks>
    /// Read by <see cref="INotificationService.CreateAsync"/> to drop the recipients
    /// who have this person on their personal blacklist. Null for a system event and
    /// for a moderation event, whose author must not be filterable.
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
