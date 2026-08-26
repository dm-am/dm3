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

    /// <summary>
    /// One guard per target type that has a rule about being subscribed to.
    /// Types without a rule contribute none, which is why the lookup is a find
    /// and not a required resolve.
    /// </summary>
    private readonly IReadOnlyCollection<ISubscriptionTargetGuard> _guards;

    public SubscriptionService(
        ISubscriptionRepository repository,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IEnumerable<ISubscriptionTargetGuard> guards)
    {
        _repository = repository;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _guards = guards.ToArray();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetMySubscriptionsAsync(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetUserSubscriptionsAsync(userId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetMySubscriptionsAsync(SubscriptionTargetType targetType, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetUserSubscriptionsAsync(userId, targetType, ct);
    }

    /// <inheritdoc />
    public async Task<Subscription> SubscribeAsync(SubscriptionTargetType targetType, Guid targetId, SubscriptionSettings? settings = null, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        // Prevent self-subscription for User type
        if (targetType == SubscriptionTargetType.User && targetId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя подписаться на себя");
        }
        // The rule the target itself puts on being subscribed to. This endpoint
        // takes a type and an identifier and belongs to no module, so it wrote
        // the row knowing nothing: a game's blacklist refuses a join, and a
        // request naming that game here walked past the refusal and put the
        // reader on the roster with the private thread open.
        var refusal = await Refusal(targetType, targetId, userId, ct);
        if (refusal is not null)
        {
            throw new HttpException(HttpStatusCode.Forbidden, refusal);
        }

        // Check if already subscribed
        var existing = await _repository.FindAsync(userId, targetType, targetId, ct);
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

        return await _repository.CreateAsync(subscription, ct);
    }

    /// <inheritdoc />
    public async Task<Subscription> UpdateSettingsAsync(Guid subscriptionId, SubscriptionSettings settings, CancellationToken ct = default)
    {
        var subscription = await _repository.GetAsync(subscriptionId, ct);

        if (subscription == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.SubscriptionNotFound);
        }

        // Verify ownership
        var userId = _identityProvider.Current.User.UserId;
        if (subscription.SubscriberId != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя изменить чужую подписку");
        }

        var update = new UpdateSubscription
        {
            SubscriptionId = subscriptionId,
            Settings = settings,
            UpdatedUtc = _dateTimeProvider.Now
        };

        return await _repository.UpdateAsync(update, ct);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await _repository.GetAsync(subscriptionId, ct);

        if (subscription == null)
        {
            return; // Already unsubscribed
        }

        // Verify ownership
        var userId = _identityProvider.Current.User.UserId;
        if (subscription.SubscriberId != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя отписать другого пользователя");
        }

        await _repository.DeleteAsync(subscriptionId, ct);
    }

    /// <inheritdoc />
    public async Task UnsubscribeByTargetAsync(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        await _repository.DeleteAsync(userId, targetType, targetId, ct);
    }

    /// <inheritdoc />
    public async Task<Subscription?> GetSubscriptionAsync(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.FindAsync(userId, targetType, targetId, ct);
    }

    /// <inheritdoc />
    public async Task<Subscription?> GetByIdAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await _repository.GetAsync(subscriptionId, ct);

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
    public async Task<int> CountTargetSubscribersAsync(SubscriptionTargetType targetType, Guid targetId,
        CancellationToken ct = default) =>
        (await _repository.GetTargetSubscriberIdsAsync(targetType, targetId, ct)).Count();

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetTargetSubscribersAsync(SubscriptionTargetType targetType, Guid targetId,
        PagingQuery query, CancellationToken ct = default)
    {
        var subscriberIds = await _repository.GetTargetSubscriberIdsAsync(targetType, targetId, ct);
        // The slice is taken before the lookups, not after: hydrating every
        // subscriber of a popular account to answer with twenty of them is one
        // round trip per subscriber for a page nobody asked for.
        var subscriberIdList = subscriberIds.Skip(query.Skip).Take(query.Take).ToList();

        if (!subscriberIdList.Any())
        {
            return Enumerable.Empty<GeneralUser>();
        }

        var users = new List<GeneralUser>();
        foreach (var subscriberId in subscriberIdList)
        {
            users.Add(await _userLookupService.GetAsync(subscriberId));
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

    /// <summary>
    /// Why the target refuses this subscription, or null when it allows it.
    /// </summary>
    private async Task<string?> Refusal(
        SubscriptionTargetType targetType, Guid targetId, Guid subscriberId, CancellationToken ct)
    {
        var guard = _guards.FirstOrDefault(g => g.TargetType == targetType);
        return guard is null ? null : await guard.Refusal(targetId, subscriberId, ct);
    }
}
