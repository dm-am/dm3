using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Subscriptions;

/// <summary>
/// Repository for subscription data access (shared across modules)
/// </summary>
public interface ISubscriptionRepository
{
    /// <summary>
    /// Get all subscriptions for a user
    /// </summary>
    Task<IEnumerable<Subscription>> GetUserSubscriptionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get user subscriptions filtered by target type
    /// </summary>
    Task<IEnumerable<Subscription>> GetUserSubscriptionsAsync(Guid userId, SubscriptionTargetType targetType, CancellationToken ct = default);

    /// <summary>
    /// Get subscription by ID
    /// </summary>
    Task<Subscription?> GetAsync(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Get subscriptions for a specific target
    /// </summary>
    Task<IEnumerable<Subscription>> GetByTargetAsync(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);

    /// <summary>
    /// Get subscriptions for a specific target with specific settings
    /// </summary>
    Task<IEnumerable<Subscription>> GetByTargetWithSettingsAsync(SubscriptionTargetType targetType, Guid targetId, SubscriptionSettings requiredSettings, CancellationToken ct = default);

    /// <summary>
    /// Find existing subscription
    /// </summary>
    Task<Subscription?> FindAsync(Guid userId, SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);

    /// <summary>
    /// Create a new subscription
    /// </summary>
    Task<Subscription> CreateAsync(CreateSubscription subscription, CancellationToken ct = default);

    /// <summary>
    /// Update subscription settings
    /// </summary>
    Task<Subscription> UpdateAsync(UpdateSubscription subscription, CancellationToken ct = default);

    /// <summary>
    /// Delete subscription
    /// </summary>
    Task DeleteAsync(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Delete subscription by user and target
    /// </summary>
    Task DeleteAsync(Guid userId, SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);

    /// <summary>
    /// Get subscriber user IDs for a target
    /// </summary>
    Task<IEnumerable<Guid>> GetTargetSubscriberIdsAsync(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);
}
