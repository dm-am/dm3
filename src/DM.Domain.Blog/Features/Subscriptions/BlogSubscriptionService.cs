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
    /// <remarks>
    /// Nobody is refused, the blog's blacklist included: the list closes writing
    /// in the blog, and a subscription delivers what its holder may read anyway.
    /// </remarks>
    public async Task<Subscription> SubscribeAsync(Guid blogId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        var existing = await _repository.FindAsync(userId, SubscriptionTargetType.Blog, blogId, ct);
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

        return await _repository.CreateAsync(subscription, ct);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(Guid blogId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        await _repository.DeleteAsync(userId, SubscriptionTargetType.Blog, blogId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserReference>> GetReadersAsync(Guid blogId, CancellationToken ct = default)
    {
        var subscriberIds = (await _repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Blog, blogId, ct))
            .ToList();
        if (subscriberIds.Count == 0)
        {
            return [];
        }

        // One read for the whole list, and the users that exist: a removed
        // subscriber does not turn the page into a 404.
        return await _userLookupService.GetReferencesAsync(subscriberIds);
    }
}
