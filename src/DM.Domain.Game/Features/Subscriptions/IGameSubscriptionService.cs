using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Subscriptions;

namespace DM.Domain.Game.Features.Subscriptions;

/// <summary>
/// Service for managing game subscriptions (readers)
/// </summary>
public interface IGameSubscriptionService
{
    /// <summary>
    /// Subscribe current user to a game as reader
    /// </summary>
    Task<Subscription> SubscribeAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribe current user from a game
    /// </summary>
    Task UnsubscribeAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Get readers (subscribers) of a game
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetReadersAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Check if a user is subscribed to a game (is a reader)
    /// </summary>
    Task<bool> IsSubscribedAsync(Guid userId, Guid gameId, CancellationToken ct = default);
}
