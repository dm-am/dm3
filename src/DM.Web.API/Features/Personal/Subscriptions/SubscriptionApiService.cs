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
    public async Task<IEnumerable<Subscription>> GetMySubscriptionsAsync()
    {
        var subscriptions = await _subscriptionService.GetMySubscriptionsAsync();
        return subscriptions.Select(_mapper.Map<Subscription>);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetMySubscriptionsAsync(SubscriptionTargetType targetType)
    {
        var subscriptions = await _subscriptionService.GetMySubscriptionsAsync(targetType);
        return subscriptions.Select(_mapper.Map<Subscription>);
    }

    /// <inheritdoc />
    public async Task<Subscription> SubscribeAsync(SubscriptionTargetType targetType, Guid targetId, SubscribeRequest? request)
    {
        var subscription = await _subscriptionService.SubscribeAsync(targetType, targetId, request?.Settings);
        return _mapper.Map<Subscription>(subscription);
    }

    /// <inheritdoc />
    public async Task<Subscription> UpdateSettingsAsync(Guid subscriptionId, UpdateSubscriptionRequest request)
    {
        var subscription = await _subscriptionService.UpdateSettingsAsync(subscriptionId, request.Settings);
        return _mapper.Map<Subscription>(subscription);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(Guid subscriptionId)
    {
        await _subscriptionService.UnsubscribeAsync(subscriptionId);
    }

    /// <inheritdoc />
    public async Task UnsubscribeByTargetAsync(SubscriptionTargetType targetType, Guid targetId)
    {
        await _subscriptionService.UnsubscribeByTargetAsync(targetType, targetId);
    }

    /// <inheritdoc />
    public async Task<Subscription?> GetSubscriptionAsync(SubscriptionTargetType targetType, Guid targetId)
    {
        var subscription = await _subscriptionService.GetSubscriptionAsync(targetType, targetId);
        return subscription == null ? null : _mapper.Map<Subscription>(subscription);
    }

    /// <inheritdoc />
    public async Task<Subscription?> GetByIdAsync(Guid subscriptionId)
    {
        var subscription = await _subscriptionService.GetByIdAsync(subscriptionId);
        return subscription == null ? null : _mapper.Map<Subscription>(subscription);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<User>> GetSubscribersAsync(SubscriptionTargetType targetType, Guid targetId)
    {
        var subscribers = await _subscriptionService.GetTargetSubscribersAsync(targetType, targetId);
        return subscribers.Select(_mapper.Map<User>);
    }
}
