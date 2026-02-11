using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Subscriptions;

/// <summary>
/// Service for subscription management
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Get all subscriptions for current user
    /// </summary>
    Task<IEnumerable<SubscriptionDto>> GetMySubscriptions(CancellationToken ct = default);

    /// <summary>
    /// Get current user subscriptions filtered by target type
    /// </summary>
    Task<IEnumerable<SubscriptionDto>> GetMySubscriptions(SubscriptionTargetType targetType, CancellationToken ct = default);

    /// <summary>
    /// Subscribe to a target
    /// </summary>
    Task<SubscriptionDto> Subscribe(SubscriptionTargetType targetType, Guid targetId, SubscriptionSettings? settings = null, CancellationToken ct = default);

    /// <summary>
    /// Update subscription settings
    /// </summary>
    Task<SubscriptionDto> UpdateSettings(Guid subscriptionId, SubscriptionSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    Task Unsubscribe(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Check if user is subscribed to a target
    /// </summary>
    Task<SubscriptionDto?> GetSubscription(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);
}

/// <summary>
/// Subscription DTO
/// </summary>
public class SubscriptionDto
{
    /// <summary>
    /// Subscription identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Target type
    /// </summary>
    public SubscriptionTargetType TargetType { get; set; }

    /// <summary>
    /// Target entity identifier
    /// </summary>
    public Guid TargetId { get; set; }

    /// <summary>
    /// Notification settings
    /// </summary>
    public SubscriptionSettings Settings { get; set; }

    /// <summary>
    /// Source of subscription
    /// </summary>
    public SubscriptionSource Source { get; set; }

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
