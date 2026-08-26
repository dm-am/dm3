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
