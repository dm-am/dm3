using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Subscriptions;

namespace DM.Domain.Game.Features.Subscriptions;

/// <summary>
/// Service for managing game subscriptions
/// </summary>
public interface IGameSubscriptionService
{
    /// <summary>
    /// Subscribe current user to a game
    /// </summary>
    Task<Subscription> SubscribeAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Subscribe another user, where the subscription follows from somebody
    /// else's action rather than from a request of their own. A blacklisted user
    /// is skipped instead of refused: the caller is not the subscriber, and a
    /// refusal would fail the action that caused the subscription.
    /// </summary>
    Task SubscribeUserAsync(Guid gameId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribe current user from a game
    /// </summary>
    Task UnsubscribeAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Get subscribers of a game
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetSubscribersAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Check if a user is subscribed to a game
    /// </summary>
    Task<bool> IsSubscribedAsync(Guid userId, Guid gameId, CancellationToken ct = default);
}
