using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Subscriptions;

/// <summary>
/// DTO for creating a subscription (repository level)
/// </summary>
public class CreateSubscription
{
    /// <summary>Subscription identifier</summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>Subscriber user identifier</summary>
    public Guid SubscriberId { get; set; }

    /// <summary>Target type</summary>
    public SubscriptionTargetType TargetType { get; set; }

    /// <summary>Target entity identifier</summary>
    public Guid TargetId { get; set; }

    /// <summary>Notification settings</summary>
    public SubscriptionSettings Settings { get; set; }

    /// <summary>Creation timestamp</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// DTO for updating a subscription (repository level)
/// </summary>
public class UpdateSubscription
{
    /// <summary>Subscription identifier</summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>Notification settings</summary>
    public SubscriptionSettings Settings { get; set; }

    /// <summary>Update timestamp</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
