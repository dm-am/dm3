using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Subscriptions;

namespace DM.Domain.Personal.Features.Subscriptions;

/// <summary>
/// Service for managing current user's subscriptions
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Get all subscriptions for current user
    /// </summary>
    Task<IEnumerable<Subscription>> GetMySubscriptionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get current user subscriptions filtered by target type
    /// </summary>
    Task<IEnumerable<Subscription>> GetMySubscriptionsAsync(SubscriptionTargetType targetType, CancellationToken ct = default);

    /// <summary>
    /// Subscribe current user to a target (User or Topic)
    /// </summary>
    /// <remarks>
    /// For Blog and Game subscriptions, use IBlogSubscriptionService and IGameSubscriptionService respectively.
    /// </remarks>
    Task<Subscription> SubscribeAsync(SubscriptionTargetType targetType, Guid targetId, SubscriptionSettings? settings = null, CancellationToken ct = default);

    /// <summary>
    /// Update subscription settings
    /// </summary>
    Task<Subscription> UpdateSettingsAsync(Guid subscriptionId, SubscriptionSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribe by subscription ID
    /// </summary>
    Task UnsubscribeAsync(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribe by target type and ID
    /// </summary>
    Task UnsubscribeByTargetAsync(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);

    /// <summary>
    /// Check if current user is subscribed to a target
    /// </summary>
    Task<Subscription?> GetSubscriptionAsync(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);

    /// <summary>
    /// Get subscription by ID
    /// </summary>
    Task<Subscription?> GetByIdAsync(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Get subscribers (users) for a target entity
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetTargetSubscribersAsync(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default);
}
