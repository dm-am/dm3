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
    public async Task<IEnumerable<User>> GetSubscribersAsync(string username)
    {
        // Get throws HttpException if user not found
        var user = await _userService.GetAsync(username);
        var subscribers = await _subscriptionService.GetTargetSubscribersAsync(SubscriptionTargetType.User, user.UserId);
        return subscribers.Select(_mapper.Map<User>);
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
