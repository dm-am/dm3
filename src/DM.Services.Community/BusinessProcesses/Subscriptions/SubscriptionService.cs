using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Subscriptions;

namespace DM.Services.Community.BusinessProcesses.Subscriptions;

/// <inheritdoc />
internal class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public SubscriptionService(
        ISubscriptionRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SubscriptionDto>> GetMySubscriptions(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var subscriptions = await _repository.GetUserSubscriptions(userId, ct);
        return subscriptions.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SubscriptionDto>> GetMySubscriptions(SubscriptionTargetType targetType, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var subscriptions = await _repository.GetUserSubscriptions(userId, targetType, ct);
        return subscriptions.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<SubscriptionDto> Subscribe(SubscriptionTargetType targetType, Guid targetId, SubscriptionSettings? settings = null, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        // Check if already subscribed
        var existing = await _repository.Find(userId, targetType, targetId, ct);
        if (existing != null)
        {
            return MapToDto(existing);
        }

        var defaultSettings = GetDefaultSettings(targetType);
        var subscription = new Subscription
        {
            SubscriptionId = _guidFactory.Create(),
            SubscriberId = userId,
            TargetType = targetType,
            TargetId = targetId,
            Settings = settings ?? defaultSettings,
            Source = SubscriptionSource.Manual,
            CreatedUtc = _dateTimeProvider.Now
        };

        var created = await _repository.Create(subscription, ct);
        return MapToDto(created);
    }

    /// <inheritdoc />
    public async Task<SubscriptionDto> UpdateSettings(Guid subscriptionId, SubscriptionSettings settings, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var subscription = await _repository.Get(subscriptionId, ct);

        if (subscription == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Subscription not found");
        }

        if (subscription.SubscriberId != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Cannot modify another user's subscription");
        }

        subscription.Settings = settings;
        subscription.UpdatedUtc = _dateTimeProvider.Now;

        var updated = await _repository.Update(subscription, ct);
        return MapToDto(updated);
    }

    /// <inheritdoc />
    public async Task Unsubscribe(Guid subscriptionId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var subscription = await _repository.Get(subscriptionId, ct);

        if (subscription == null)
        {
            return; // Already unsubscribed
        }

        if (subscription.SubscriberId != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Cannot delete another user's subscription");
        }

        await _repository.Delete(subscriptionId, ct);
    }

    /// <inheritdoc />
    public async Task<SubscriptionDto?> GetSubscription(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var subscription = await _repository.Find(userId, targetType, targetId, ct);
        return subscription == null ? null : MapToDto(subscription);
    }

    private static SubscriptionDto MapToDto(Subscription subscription) => new()
    {
        Id = subscription.SubscriptionId,
        TargetType = subscription.TargetType,
        TargetId = subscription.TargetId,
        Settings = subscription.Settings,
        Source = subscription.Source,
        CreatedUtc = subscription.CreatedUtc
    };

    private static SubscriptionSettings GetDefaultSettings(SubscriptionTargetType targetType) => targetType switch
    {
        SubscriptionTargetType.Game => SubscriptionSettings.GameReaderDefault,
        SubscriptionTargetType.Blog => SubscriptionSettings.BlogReaderDefault,
        SubscriptionTargetType.Topic => SubscriptionSettings.TopicDefault,
        SubscriptionTargetType.Author => SubscriptionSettings.AuthorNewContent | SubscriptionSettings.InApp,
        SubscriptionTargetType.Board => SubscriptionSettings.NewTopics | SubscriptionSettings.InApp,
        SubscriptionTargetType.Publication => SubscriptionSettings.NewComments | SubscriptionSettings.InApp,
        SubscriptionTargetType.GameDiscussion => SubscriptionSettings.NewComments | SubscriptionSettings.InApp,
        SubscriptionTargetType.BlogDiscussion => SubscriptionSettings.NewComments | SubscriptionSettings.InApp,
        _ => SubscriptionSettings.InApp
    };
}
