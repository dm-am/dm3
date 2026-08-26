using DM.Domain.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Personal.Notifications;

/// <summary>
/// DAL model for real-time notifications: one row per logical notification,
/// recipients normalized into <see cref="NotificationRecipient"/>.
/// </summary>
[Table("Notifications")]
public class Notification
{
    /// <summary>
    /// Notification identifier
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Identifier of the bus publication the notification was created for.
    /// Unique together with <see cref="EventType"/> (partial, where not null):
    /// one event fans out to at most one notification per output type, which is
    /// the invariant the idempotent write of a redelivered event leans on. Null
    /// for rows born from messages that predate the key.
    /// </summary>
    public Guid? EventId { get; set; }

    /// <summary>
    /// Event type
    /// </summary>
    public EventType EventType { get; set; }

    /// <summary>
    /// Creation moment. What the retention sweep reads.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Notification metadata as serialized JSON. The shape depends on the event
    /// type and nothing queries inside it, hence one jsonb column.
    /// </summary>
    public string Metadata { get; set; } = null!;

    /// <summary>
    /// Recipients of the notification
    /// </summary>
    public List<NotificationRecipient> Recipients { get; set; } = [];
}

/// <summary>
/// DAL model for one recipient of a notification. Presence of the row is
/// "interested"; <see cref="IsRead"/> is "already notified".
/// </summary>
[Table("NotificationRecipients")]
public class NotificationRecipient
{
    /// <summary>
    /// Notification identifier, part of the primary key
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Recipient user identifier, part of the primary key
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Whether the recipient has read the notification
    /// </summary>
    public bool IsRead { get; set; }
}
