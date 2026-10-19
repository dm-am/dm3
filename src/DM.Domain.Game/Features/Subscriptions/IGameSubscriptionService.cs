using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Subscriptions;

namespace DM.Domain.Game.Features.Subscriptions;

public interface IGameSubscriptionService
{
    Task<Subscription> SubscribeAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Follows from somebody else's action: the player who lost his last character stays as a reader.
    /// </summary>
    Task SubscribeUserAsync(Guid gameId, Guid userId, CancellationToken ct = default);

    Task UnsubscribeAsync(Guid gameId, CancellationToken ct = default);

    Task<IEnumerable<UserReference>> GetSubscribersAsync(Guid gameId, CancellationToken ct = default);
}
