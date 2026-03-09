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
    Task<Subscription> Subscribe(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribe current user from a game
    /// </summary>
    Task Unsubscribe(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Get readers (subscribers) of a game
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetReaders(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Check if a user is subscribed to a game (is a reader)
    /// </summary>
    Task<bool> IsSubscribed(Guid userId, Guid gameId, CancellationToken ct = default);
}
