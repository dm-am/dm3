using DM.Domain.Core.Enums;
using System;
using System.Collections.Generic;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Infrastructure.Persistence.Entities.Notifications;

/// <summary>
/// DAL model for real-time notifications
/// </summary>
[MongoCollectionName("RealtimeNotifications")]
public class Notification
{
    /// <summary>
    /// Notification identifier
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// List of users who should receive the notification
    /// </summary>
    public IEnumerable<Guid> UsersInterested { get; set; } = [];

    /// <summary>
    /// List of users who already received the notification
    /// </summary>
    public IEnumerable<Guid> UsersNotified { get; set; } = [];

    /// <summary>
    /// Notification metadata
    /// </summary>
    public object Metadata { get; set; } = null!;

    /// <summary>
    /// Event type
    /// </summary>
    public EventType EventType { get; set; }
}