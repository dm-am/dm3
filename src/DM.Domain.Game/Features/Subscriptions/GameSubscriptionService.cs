using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.Blacklists;

namespace DM.Domain.Game.Features.Subscriptions;

/// <inheritdoc />
internal class GameSubscriptionService : IGameSubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGameBlacklistRepository _blacklistRepository;

    public GameSubscriptionService(
        ISubscriptionRepository repository,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IGameBlacklistRepository blacklistRepository)
    {
        _repository = repository;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _blacklistRepository = blacklistRepository;
    }

    /// <inheritdoc />
    public async Task<Subscription> SubscribeAsync(Guid gameId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        // Subscribing is writing: it puts the user on the game's roster and hands
        // them GameRole.Reader, which opens the private comment thread. Reading the
        // game is open to a blacklisted user and stays open, joining it is what the
        // blacklist refuses. GameBlacklistService refuses to blacklist a subscriber
        // at all and makes the owner remove them first, so without this the same
        // invariant could be walked back from the other side by one request.
        if (await _blacklistRepository.IsBlocked(gameId, userId, ct))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromGame);
        }

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
