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

namespace DM.Domain.Blog.Features.Subscriptions;

/// <inheritdoc />
internal class BlogSubscriptionService : IBlogSubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BlogSubscriptionService(
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
    public async Task<Subscription> Subscribe(Guid blogId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        // Check if already subscribed
        var existing = await _repository.Find(userId, SubscriptionTargetType.Blog, blogId, ct);
        if (existing != null)
        {
            return existing;
        }

        var subscription = new CreateSubscription
        {
            SubscriptionId = _guidFactory.Create(),
            SubscriberId = userId,
            TargetType = SubscriptionTargetType.Blog,
            TargetId = blogId,
            Settings = SubscriptionSettings.BlogReaderDefault,
            CreatedUtc = _dateTimeProvider.Now
        };

        return await _repository.Create(subscription, ct);
    }

    /// <inheritdoc />
    public async Task Unsubscribe(Guid blogId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        await _repository.Delete(userId, SubscriptionTargetType.Blog, blogId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetReaders(Guid blogId, CancellationToken ct = default)
    {
        var subscriberIds = await _repository.GetTargetSubscriberIds(SubscriptionTargetType.Blog, blogId, ct);
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

    /// <inheritdoc />
    public async Task<bool> IsSubscribed(Guid userId, Guid blogId, CancellationToken ct = default)
    {
        var subscription = await _repository.Find(userId, SubscriptionTargetType.Blog, blogId, ct);
        return subscription != null;
    }
}
