using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Subscriptions;

/// <summary>
/// Subscription DTO
/// </summary>
public class Subscription
{
    /// <summary>Subscription identifier</summary>
    public Guid Id { get; set; }

    /// <summary>Subscriber user identifier</summary>
    public Guid SubscriberId { get; set; }

    /// <summary>Target type</summary>
    public SubscriptionTargetType TargetType { get; set; }

    /// <summary>Target entity identifier</summary>
    public Guid TargetId { get; set; }

    /// <summary>Notification settings</summary>
    public SubscriptionSettings Settings { get; set; }

    /// <summary>Creation date</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
