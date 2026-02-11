using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Subscriptions;

/// <summary>
/// Subscription DTO for API responses
/// </summary>
public class Subscription
{
    /// <summary>
    /// Subscription identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Target type (Game, Blog, Author, Board, etc.)
    /// </summary>
    public SubscriptionTargetType TargetType { get; set; }

    /// <summary>
    /// Target entity identifier
    /// </summary>
    public Guid TargetId { get; set; }

    /// <summary>
    /// Notification settings flags
    /// </summary>
    public SubscriptionSettings Settings { get; set; }

    /// <summary>
    /// Source of subscription (Manual or Participation)
    /// </summary>
    public SubscriptionSource Source { get; set; }

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
