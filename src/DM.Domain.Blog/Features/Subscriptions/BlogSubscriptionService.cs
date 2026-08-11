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
    public async Task<Subscription> SubscribeAsync(Guid blogId, CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;

        // Check if already subscribed
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
        var subscriberIds = await _repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Blog, blogId, ct);
        var subscriberIdList = subscriberIds.ToList();

        if (!subscriberIdList.Any())
        {
            return Enumerable.Empty<UserReference>();
        }

        // One read for the whole list. Asked one at a time, a hundred subscribers
        // were a hundred round trips, and GetAsync throws on a user who is no
        // longer there — a single removed subscriber answered the entire page
        // with 410 Gone. The batch form returns the users that exist and says
        // nothing about the ones that do not, which is what a list of readers
        // needs.
        return await _userLookupService.GetReferencesAsync(subscriberIdList);
    }

    /// <inheritdoc />
    public async Task<bool> IsSubscribedAsync(Guid userId, Guid blogId, CancellationToken ct = default)
    {
        var subscription = await _repository.FindAsync(userId, SubscriptionTargetType.Blog, blogId, ct);
        return subscription != null;
    }
}
