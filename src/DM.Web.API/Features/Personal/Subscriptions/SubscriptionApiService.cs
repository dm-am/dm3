using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Personal.Features.Subscriptions;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Personal.Subscriptions;

/// <inheritdoc />
internal class SubscriptionApiService : ISubscriptionApiService
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public SubscriptionApiService(
        ISubscriptionService subscriptionService,
        IMapper mapper)
    {
        _subscriptionService = subscriptionService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetMySubscriptions()
    {
        var subscriptions = await _subscriptionService.GetMySubscriptions();
        return subscriptions.Select(_mapper.Map<Subscription>);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetMySubscriptions(SubscriptionTargetType targetType)
    {
        var subscriptions = await _subscriptionService.GetMySubscriptions(targetType);
        return subscriptions.Select(_mapper.Map<Subscription>);
    }

    /// <inheritdoc />
    public async Task<Subscription> Subscribe(SubscriptionTargetType targetType, Guid targetId, SubscribeRequest? request)
    {
        var subscription = await _subscriptionService.Subscribe(targetType, targetId, request?.Settings);
        return _mapper.Map<Subscription>(subscription);
    }

    /// <inheritdoc />
    public async Task<Subscription> UpdateSettings(Guid subscriptionId, UpdateSubscriptionRequest request)
    {
        var subscription = await _subscriptionService.UpdateSettings(subscriptionId, request.Settings);
        return _mapper.Map<Subscription>(subscription);
    }

    /// <inheritdoc />
    public async Task Unsubscribe(Guid subscriptionId)
    {
        await _subscriptionService.Unsubscribe(subscriptionId);
    }

    /// <inheritdoc />
    public async Task UnsubscribeByTarget(SubscriptionTargetType targetType, Guid targetId)
    {
        await _subscriptionService.UnsubscribeByTarget(targetType, targetId);
    }

    /// <inheritdoc />
    public async Task<Subscription?> GetSubscription(SubscriptionTargetType targetType, Guid targetId)
    {
        var subscription = await _subscriptionService.GetSubscription(targetType, targetId);
        return subscription == null ? null : _mapper.Map<Subscription>(subscription);
    }

    /// <inheritdoc />
    public async Task<Subscription?> GetById(Guid subscriptionId)
    {
        var subscription = await _subscriptionService.GetById(subscriptionId);
        return subscription == null ? null : _mapper.Map<Subscription>(subscription);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<User>> GetSubscribers(SubscriptionTargetType targetType, Guid targetId)
    {
        var subscribers = await _subscriptionService.GetTargetSubscribers(targetType, targetId);
        return subscribers.Select(_mapper.Map<User>);
    }
}
