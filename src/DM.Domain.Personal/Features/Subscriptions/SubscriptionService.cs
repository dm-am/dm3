using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;

namespace DM.Domain.Personal.Features.Subscriptions;

/// <inheritdoc />
internal class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SubscriptionService(
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
    public async Task<IEnumerable<Subscription>> GetMySubscriptions(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetUserSubscriptions(userId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetMySubscriptions(SubscriptionTargetType targetType, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetUserSubscriptions(userId, targetType, ct);
    }

    /// <inheritdoc />
    public async Task<Subscription> Subscribe(SubscriptionTargetType targetType, Guid targetId, SubscriptionSettings? settings = null, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        // Prevent self-subscription for User type
        if (targetType == SubscriptionTargetType.User && targetId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Cannot subscribe to yourself");
        }

        // Check if already subscribed
        var existing = await _repository.Find(userId, targetType, targetId, ct);
        if (existing != null)
        {
            return existing;
        }

        var defaultSettings = GetDefaultSettings(targetType);
        var subscription = new CreateSubscription
        {
            SubscriptionId = _guidFactory.Create(),
            SubscriberId = userId,
            TargetType = targetType,
            TargetId = targetId,
            Settings = settings ?? defaultSettings,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.Create(subscription, ct);
    }

    /// <inheritdoc />
    public async Task<Subscription> UpdateSettings(Guid subscriptionId, SubscriptionSettings settings, CancellationToken ct = default)
    {
        var subscription = await _repository.Get(subscriptionId, ct);

        if (subscription == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Subscription not found");
        }

        // Verify ownership
        var userId = _identityProvider.Current.User.UserId;
        if (subscription.SubscriberId != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Cannot modify another user's subscription");
        }

        var update = new UpdateSubscription
        {
            SubscriptionId = subscriptionId,
            Settings = settings,
            UpdatedUtc = _dateTimeProvider.Now
        };

        return await _repository.Update(update, ct);
    }

    /// <inheritdoc />
    public async Task Unsubscribe(Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await _repository.Get(subscriptionId, ct);

        if (subscription == null)
        {
            return; // Already unsubscribed
        }

        // Verify ownership
        var userId = _identityProvider.Current.User.UserId;
        if (subscription.SubscriberId != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Cannot unsubscribe another user");
        }

        await _repository.Delete(subscriptionId, ct);
    }

    /// <inheritdoc />
    public async Task UnsubscribeByTarget(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        await _repository.Delete(userId, targetType, targetId, ct);
    }

    /// <inheritdoc />
    public async Task<Subscription?> GetSubscription(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.Find(userId, targetType, targetId, ct);
    }

    /// <inheritdoc />
    public async Task<Subscription?> GetById(Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await _repository.Get(subscriptionId, ct);

        // Verify ownership
        if (subscription != null)
        {
            var userId = _identityProvider.Current.User.UserId;
            if (subscription.SubscriberId != userId)
            {
                return null; // Hide subscriptions belonging to other users
            }
        }

        return subscription;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetTargetSubscribers(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        var subscriberIds = await _repository.GetTargetSubscriberIds(targetType, targetId, ct);
        var subscriberIdList = subscriberIds.ToList();

        if (!subscriberIdList.Any())
        {
            return Enumerable.Empty<GeneralUser>();
        }

        var users = new List<GeneralUser>();
        foreach (var subscriberId in subscriberIdList)
        {
            var user = await _userLookupService.Get(subscriberId);
            if (user != null)
            {
                users.Add(user);
            }
        }

        return users;
    }

    private static SubscriptionSettings GetDefaultSettings(SubscriptionTargetType targetType) => targetType switch
    {
        SubscriptionTargetType.Game => SubscriptionSettings.GameReaderDefault,
        SubscriptionTargetType.Blog => SubscriptionSettings.BlogReaderDefault,
        SubscriptionTargetType.Topic => SubscriptionSettings.TopicDefault,
        SubscriptionTargetType.User => SubscriptionSettings.UserSubscriptionDefault,
        _ => SubscriptionSettings.InApp
    };
}
