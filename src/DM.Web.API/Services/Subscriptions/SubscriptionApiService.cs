using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Subscriptions;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Subscriptions;

namespace DM.Web.API.Services.Subscriptions;

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
    public async Task<ListEnvelope<Subscription>> GetMySubscriptions()
    {
        var subscriptions = await _subscriptionService.GetMySubscriptions();
        return new ListEnvelope<Subscription>(subscriptions.Select(_mapper.Map<Subscription>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Subscription>> GetMySubscriptions(SubscriptionTargetType targetType)
    {
        var subscriptions = await _subscriptionService.GetMySubscriptions(targetType);
        return new ListEnvelope<Subscription>(subscriptions.Select(_mapper.Map<Subscription>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Subscription>> Subscribe(SubscriptionTargetType targetType, Guid targetId, SubscribeRequest? request)
    {
        var subscription = await _subscriptionService.Subscribe(targetType, targetId, request?.Settings);
        return new Envelope<Subscription>(_mapper.Map<Subscription>(subscription));
    }

    /// <inheritdoc />
    public async Task<Envelope<Subscription>> UpdateSettings(Guid subscriptionId, UpdateSubscriptionRequest request)
    {
        var subscription = await _subscriptionService.UpdateSettings(subscriptionId, request.Settings);
        return new Envelope<Subscription>(_mapper.Map<Subscription>(subscription));
    }

    /// <inheritdoc />
    public async Task Unsubscribe(Guid subscriptionId)
    {
        await _subscriptionService.Unsubscribe(subscriptionId);
    }

    /// <inheritdoc />
    public async Task<Envelope<Subscription>?> GetSubscription(SubscriptionTargetType targetType, Guid targetId)
    {
        var subscription = await _subscriptionService.GetSubscription(targetType, targetId);
        return subscription == null ? null : new Envelope<Subscription>(_mapper.Map<Subscription>(subscription));
    }
}
