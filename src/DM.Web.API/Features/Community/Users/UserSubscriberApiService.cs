using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Personal.Features.Subscriptions;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Personal.Subscriptions;

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
    public async Task<IEnumerable<User>> GetSubscribers(string username)
    {
        // Get throws HttpException if user not found
        var user = await _userService.Get(username);
        var subscribers = await _subscriptionService.GetTargetSubscribers(SubscriptionTargetType.User, user.UserId);
        return subscribers.Select(_mapper.Map<User>);
    }

    /// <inheritdoc />
    public async Task<Subscription> Subscribe(string username)
    {
        var user = await _userService.Get(username);
        var subscription = await _subscriptionService.Subscribe(SubscriptionTargetType.User, user.UserId);
        return _mapper.Map<Subscription>(subscription);
    }

    /// <inheritdoc />
    public async Task Unsubscribe(string username)
    {
        var user = await _userService.Get(username);
        await _subscriptionService.UnsubscribeByTarget(SubscriptionTargetType.User, user.UserId);
    }

    /// <inheritdoc />
    public async Task<Subscription?> GetSubscriptionStatus(string username)
    {
        var user = await _userService.Get(username);
        var subscription = await _subscriptionService.GetSubscription(SubscriptionTargetType.User, user.UserId);
        return subscription == null ? null : _mapper.Map<Subscription>(subscription);
    }
}
