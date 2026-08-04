using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Subscriptions;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// API service for user subscriber operations
/// </summary>
public interface IUserSubscriberApiService
{
    /// <summary>
    /// Get a page of the subscribers of a user
    /// </summary>
    Task<ListEnvelope<User>> GetSubscribersAsync(string username, PagingQuery query);

    /// <summary>
    /// Subscribe to a user
    /// </summary>
    Task<Subscription> SubscribeAsync(string username);

    /// <summary>
    /// Unsubscribe from a user
    /// </summary>
    Task UnsubscribeAsync(string username);

    /// <summary>
    /// Check if current user is subscribed to specified user
    /// </summary>
    Task<Subscription?> GetSubscriptionStatusAsync(string username);
}
