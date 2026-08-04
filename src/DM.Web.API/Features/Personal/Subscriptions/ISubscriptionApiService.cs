using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Personal.Subscriptions;

/// <summary>
/// API service for subscription management
/// </summary>
public interface ISubscriptionApiService
{
    /// <summary>
    /// Get all subscriptions for the current user
    /// </summary>
    Task<IEnumerable<Subscription>> GetMySubscriptionsAsync();

    /// <summary>
    /// Get current user subscriptions filtered by target type
    /// </summary>
    Task<IEnumerable<Subscription>> GetMySubscriptionsAsync(SubscriptionTargetType targetType);

    /// <summary>
    /// Subscribe to a target
    /// </summary>
    Task<Subscription> SubscribeAsync(SubscriptionTargetType targetType, Guid targetId, SubscribeRequest? request);

    /// <summary>
    /// Update subscription settings
    /// </summary>
    Task<Subscription> UpdateSettingsAsync(Guid subscriptionId, UpdateSubscriptionRequest request);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    Task UnsubscribeAsync(Guid subscriptionId);

    /// <summary>
    /// Unsubscribe by target type and ID
    /// </summary>
    Task UnsubscribeByTargetAsync(SubscriptionTargetType targetType, Guid targetId);

    /// <summary>
    /// Check if user is subscribed to a target
    /// </summary>
    Task<Subscription?> GetSubscriptionAsync(SubscriptionTargetType targetType, Guid targetId);

    /// <summary>
    /// Get subscription by ID
    /// </summary>
    Task<Subscription?> GetByIdAsync(Guid subscriptionId);
}
