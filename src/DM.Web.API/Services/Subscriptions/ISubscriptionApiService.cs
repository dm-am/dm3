using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Subscriptions;

namespace DM.Web.API.Services.Subscriptions;

/// <summary>
/// API service for subscription management
/// </summary>
public interface ISubscriptionApiService
{
    /// <summary>
    /// Get all subscriptions for the current user
    /// </summary>
    Task<ListEnvelope<Subscription>> GetMySubscriptions();

    /// <summary>
    /// Get current user subscriptions filtered by target type
    /// </summary>
    Task<ListEnvelope<Subscription>> GetMySubscriptions(SubscriptionTargetType targetType);

    /// <summary>
    /// Subscribe to a target
    /// </summary>
    Task<Envelope<Subscription>> Subscribe(SubscriptionTargetType targetType, Guid targetId, SubscribeRequest? request);

    /// <summary>
    /// Update subscription settings
    /// </summary>
    Task<Envelope<Subscription>> UpdateSettings(Guid subscriptionId, UpdateSubscriptionRequest request);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    Task Unsubscribe(Guid subscriptionId);

    /// <summary>
    /// Check if user is subscribed to a target
    /// </summary>
    Task<Envelope<Subscription>?> GetSubscription(SubscriptionTargetType targetType, Guid targetId);
}
