using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Subscriptions;

/// <summary>
/// DAL model for content subscription
/// </summary>
[Table("Subscriptions")]
public class Subscription
{
    /// <summary>
    /// Subscription identifier
    /// </summary>
    [Key]
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// Subscriber user identifier
    /// </summary>
    public Guid SubscriberId { get; set; }

    /// <summary>
    /// Type of subscribed entity
    /// </summary>
    public SubscriptionTargetType TargetType { get; set; }

    /// <summary>
    /// Target entity identifier (GameId, BlogId, UserId, BoardId, TopicId, etc.)
    /// </summary>
    public Guid TargetId { get; set; }

    /// <summary>
    /// Notification settings (flags)
    /// </summary>
    public SubscriptionSettings Settings { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last settings update moment (UTC)
    /// </summary>
    public DateTimeOffset? UpdatedUtc { get; set; }

    /// <summary>
    /// Subscriber user
    /// </summary>
    [ForeignKey(nameof(SubscriberId))]
    public virtual User Subscriber { get; set; } = null!;
}
