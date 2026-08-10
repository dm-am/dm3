using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Game.Features.Blacklists;

namespace DM.Domain.Game.Features.Subscriptions;

/// <inheritdoc />
/// <remarks>
/// The one place the game's rule on being subscribed to is written. Both handles
/// that create a game subscription ask it: the game's own and the generic
/// endpoint, which previously created the row knowing nothing about blacklists.
///
/// Subscribing is writing. It puts the user on the game's roster and hands them
/// GameRole.Reader, which opens the private comment thread. Reading the game is
/// open to a blacklisted user and stays open; joining it is what the blacklist
/// refuses. GameBlacklistService refuses to blacklist a subscriber at all and
/// makes the owner remove them first, so without this the same invariant could
/// be walked back from the other side by one request.
/// </remarks>
internal class GameSubscriptionGuard : ISubscriptionTargetGuard
{
    private readonly IGameBlacklistRepository _blacklistRepository;

    public GameSubscriptionGuard(IGameBlacklistRepository blacklistRepository)
    {
        _blacklistRepository = blacklistRepository;
    }

    /// <inheritdoc />
    public SubscriptionTargetType TargetType => SubscriptionTargetType.Game;

    /// <inheritdoc />
    public async Task<string?> Refusal(Guid targetId, Guid subscriberId, CancellationToken ct = default) =>
        await _blacklistRepository.IsBlocked(targetId, subscriberId, ct)
            ? RefusalMessage.BlacklistedFromGame
            : null;
}
