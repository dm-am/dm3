using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Subscriptions;

namespace DM.Services.Community.BusinessProcesses.Subscriptions;

/// <summary>
/// Repository for subscription data access
/// </summary>
public interface ISubscriptionRepository
{
    /// <summary>
    /// Get all subscriptions for a user
    /// </summary>
    Task<IEnumerable<Subscription>> GetUserSubscriptions(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get user subscriptions filtered by target type
    /// </summary>
    Task<IEnumerable<Subscription>> GetUserSubscriptions(Guid userId, SubscriptionTargetType targetType, CancellationToken ct = default);

    /// <summary>
    /// Get subscription by ID
    /// </summary>
    Task<Subscription?> Get(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Get subscriptions for a specific target
    /// </summary>
    Task<IEnumerable<Subscription>> GetByTarget(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);

    /// <summary>
    /// Get subscriptions for a specific target with specific settings
    /// </summary>
    Task<IEnumerable<Subscription>> GetByTargetWithSettings(SubscriptionTargetType targetType, Guid targetId, SubscriptionSettings requiredSettings, CancellationToken ct = default);

    /// <summary>
    /// Find existing subscription
    /// </summary>
    Task<Subscription?> Find(Guid userId, SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);

    /// <summary>
    /// Create a new subscription
    /// </summary>
    Task<Subscription> Create(Subscription subscription, CancellationToken ct = default);

    /// <summary>
    /// Update subscription settings
    /// </summary>
    Task<Subscription> Update(Subscription subscription, CancellationToken ct = default);

    /// <summary>
    /// Delete subscription
    /// </summary>
    Task Delete(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Delete all participation-based subscriptions for a target
    /// </summary>
    Task DeleteParticipationSubscriptions(Guid userId, SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);
}
