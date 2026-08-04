using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Personal.Features.Subscriptions;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;
using DM.Web.API.Features.Personal.Subscriptions;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Users;

/// <inheritdoc />
internal class UserSubscriberApiService : IUserSubscriberApiService
{
    private readonly IUserService _userService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IMapper _mapper;

    public UserSubscriberApiService(
        IUserService userService,
        ISubscriptionService subscriptionService,
        IMapper mapper)
    {
        _userService = userService;
        _subscriptionService = subscriptionService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Paged, and the page is what leaves the process. The endpoint is public
    /// and used to answer with every subscriber of the addressed account, each
    /// one a full User, so the size and the cost of the answer grew with
    /// somebody else's popularity and no caller could ask for less.
    ///
    /// The identifiers still come back in one query - they are indexed and
    /// cheap - and the slice is taken before the users are hydrated, which is
    /// the expensive half: a page of twenty costs twenty lookups whatever the
    /// account's following looks like.
    /// </remarks>
    public async Task<ListEnvelope<User>> GetSubscribersAsync(string username, PagingQuery query)
    {
        // Get throws HttpException if user not found
        var user = await _userService.GetAsync(username);
        var subscribers = (await _subscriptionService
            .GetTargetSubscribersAsync(SubscriptionTargetType.User, user.UserId, query))
            .ToList();
        var total = await _subscriptionService
            .CountTargetSubscribersAsync(SubscriptionTargetType.User, user.UserId);
        return new ListEnvelope<User>(
            subscribers.Select(_mapper.Map<User>),
            new PagingInfo(query.Skip, query.Take, total));
    }

    /// <inheritdoc />
    public async Task<Subscription> SubscribeAsync(string username)
    {
        var user = await _userService.GetAsync(username);
        var subscription = await _subscriptionService.SubscribeAsync(SubscriptionTargetType.User, user.UserId);
        return _mapper.Map<Subscription>(subscription);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string username)
    {
        var user = await _userService.GetAsync(username);
        await _subscriptionService.UnsubscribeByTargetAsync(SubscriptionTargetType.User, user.UserId);
    }

    /// <inheritdoc />
    public async Task<Subscription?> GetSubscriptionStatusAsync(string username)
    {
        var user = await _userService.GetAsync(username);
        var subscription = await _subscriptionService.GetSubscriptionAsync(SubscriptionTargetType.User, user.UserId);
        return subscription == null ? null : _mapper.Map<Subscription>(subscription);
    }
}
