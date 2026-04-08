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
    public async Task<Subscription> SubscribeAsync(Guid gameId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

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
    public async Task<IEnumerable<GeneralUser>> GetSubscribersAsync(Guid gameId, CancellationToken ct = default)
    {
        var subscriberIds = await _repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Game, gameId, ct);
        var subscriberIdList = subscriberIds.ToList();

        if (!subscriberIdList.Any())
        {
            return Enumerable.Empty<GeneralUser>();
        }

        var users = new List<GeneralUser>();
        foreach (var subscriberId in subscriberIdList)
        {
            var user = await _userLookupService.GetAsync(subscriberId);
            if (user != null)
            {
                users.Add(user);
            }
        }

        return users;
    }

    /// <inheritdoc />
    public async Task<bool> IsSubscribedAsync(Guid userId, Guid gameId, CancellationToken ct = default)
    {
        var subscription = await _repository.FindAsync(userId, SubscriptionTargetType.Game, gameId, ct);
        return subscription != null;
    }
}
