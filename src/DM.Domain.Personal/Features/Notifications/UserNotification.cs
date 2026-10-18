using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// DTO model of received notification
/// </summary>
public class UserNotification
{
    /// <summary>
    /// Notification identifier
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Event type
    /// </summary>
    public EventType EventType { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Notification metadata
    /// </summary>
    public object Metadata { get; set; } = null!;
}

/// <summary>
/// A notification as it stands in the list of one of its recipients
/// </summary>
/// <remarks>
/// Whether it was read belongs to the recipient and not to the notification: one
/// stored notification reaches several people, and each of them reads it in their
/// own time. The mark is not on UserNotification itself because the push derives
/// from it, and a push has many recipients and no reader yet.
/// </remarks>
public class ListedNotification : UserNotification
{
    /// <summary>
    /// Whether the recipient whose list this is has read it
    /// </summary>
    public bool IsRead { get; set; }
}

/// <summary>
/// Entity DTO for creating a notification (repository level)
/// </summary>
public class CreateNotificationEntity
{
    /// <summary>
    /// Notification identifier
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// List of users who should receive the notification
    /// </summary>
    public IEnumerable<Guid> UsersInterested { get; set; } = [];

    /// <summary>
    /// Notification metadata
    /// </summary>
    public object Metadata { get; set; } = null!;

    /// <summary>
    /// Event type
    /// </summary>
    public EventType EventType { get; set; }

    /// <summary>
    /// Identity of the bus publication the notification was created for, carried
    /// to the stored row so a redelivery of the same publication finds it there
    /// </summary>
    public Guid? EventId { get; set; }
}

/// <summary>
/// DTO model for notification that is ready to be received
/// </summary>
public class RealtimeNotification : UserNotification
{
    /// <summary>
    /// Identifiers of users who might be interested in this notification
    /// </summary>
    public IEnumerable<Guid> RecipientIds { get; set; } = [];
}
