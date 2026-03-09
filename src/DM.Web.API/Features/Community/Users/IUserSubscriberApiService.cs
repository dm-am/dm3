using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Features.Personal.Subscriptions;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// API service for user subscriber operations
/// </summary>
public interface IUserSubscriberApiService
{
    /// <summary>
    /// Get subscribers of a user
    /// </summary>
    Task<IEnumerable<User>> GetSubscribers(string username);

    /// <summary>
    /// Subscribe to a user
    /// </summary>
    Task<Subscription> Subscribe(string username);

    /// <summary>
    /// Unsubscribe from a user
    /// </summary>
    Task Unsubscribe(string username);

    /// <summary>
    /// Check if current user is subscribed to specified user
    /// </summary>
    Task<Subscription?> GetSubscriptionStatus(string username);
}
