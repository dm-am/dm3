using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;

namespace DM.Domain.Game.Features.Subscriptions;

/// <inheritdoc />
internal class GameSubscriptionService : IGameSubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GameSubscriptionService(
        ISubscriptionRepository repository,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Nobody is refused, the game's blacklist included. The list closes the
    /// discussion and the application; a subscription delivers what its holder
    /// may read anyway, and the role of reader it gives opens neither of the two -
    /// both ask the list itself (GameRoleExtensions.IsBlacklisted).
    /// </remarks>
    public Task<Subscription> SubscribeAsync(Guid gameId, CancellationToken ct = default) =>
        SubscribeInternal(gameId, _identityProvider.Current.User.UserId, ct);

    /// <inheritdoc />
    public async Task SubscribeUserAsync(Guid gameId, Guid userId, CancellationToken ct = default) =>
        await SubscribeInternal(gameId, userId, ct);

    private async Task<Subscription> SubscribeInternal(Guid gameId, Guid userId, CancellationToken ct)
    {
        // Check if already subscribed
        var existing = await _repository.FindAsync(userId, SubscriptionTargetType.Game, gameId, ct);
        if (existing != null)
        {
            return existing;
        }

        var subscription = new CreateSubscription
        {
            SubscriptionId = _guidFactory.Create(),
            SubscriberId = userId,
            TargetType = SubscriptionTargetType.Game,
            TargetId = gameId,
            Settings = SubscriptionSettings.GameReaderDefault,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.CreateAsync(subscription, ct);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(Guid gameId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        await _repository.DeleteAsync(userId, SubscriptionTargetType.Game, gameId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserReference>> GetSubscribersAsync(Guid gameId, CancellationToken ct = default)
    {
        var subscriberIds = await _repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Game, gameId, ct);
        var subscriberIdList = subscriberIds.ToList();

        if (!subscriberIdList.Any())
        {
            return Enumerable.Empty<UserReference>();
        }

        // One read for the whole list. Asked one at a time, a hundred subscribers
        // were a hundred round trips, and GetAsync throws on a user who is no
        // longer there — a single removed subscriber answered the entire page
        // with 404. The batch form returns the users that exist and says
        // nothing about the ones that do not, which is what a list of readers
        // needs.
        return await _userLookupService.GetReferencesAsync(subscriberIdList);
    }

    /// <inheritdoc />
    public async Task<bool> IsSubscribedAsync(Guid userId, Guid gameId, CancellationToken ct = default)
    {
        var subscription = await _repository.FindAsync(userId, SubscriptionTargetType.Game, gameId, ct);
        return subscription != null;
    }
}
