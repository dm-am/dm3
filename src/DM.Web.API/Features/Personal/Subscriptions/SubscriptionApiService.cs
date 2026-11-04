using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;
using DM.Domain.Personal.Features.Subscriptions;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Subscriptions;

public class SubscriptionApiService
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IGameService _gameService;
    private readonly SubscriptionMapper _mapper;

    public SubscriptionApiService(
        ISubscriptionService subscriptionService,
        IGameService gameService,
        SubscriptionMapper mapper)
    {
        _subscriptionService = subscriptionService;
        _gameService = gameService;
        _mapper = mapper;
    }

    public async Task<IEnumerable<Subscription>> GetMySubscriptionsAsync(SubscriptionTargetType? targetType)
    {
        var subscriptions = targetType.HasValue
            ? await _subscriptionService.GetMySubscriptionsAsync(targetType.Value)
            : await _subscriptionService.GetMySubscriptionsAsync();
        return subscriptions.Select(_mapper.ToSubscription);
    }

    public async Task<Subscription> SubscribeAsync(SubscriptionTargetType targetType, Guid targetId, SubscribeRequest? request)
    {
        // The personal service knows nothing of games, so the game is read here,
        // through the reader's own scope: a game hidden from the reader answers
        // the same 404 an identifier that addresses nothing does.
        if (targetType == SubscriptionTargetType.Game)
        {
            await _gameService.GetAsync(targetId);
        }

        var subscription = await _subscriptionService.SubscribeAsync(targetType, targetId, request?.Settings);
        return _mapper.ToSubscription(subscription);
    }

    public async Task<Subscription> UpdateSettingsAsync(Guid subscriptionId, UpdateSubscriptionRequest request)
    {
        var subscription = await _subscriptionService.UpdateSettingsAsync(subscriptionId, request.Settings);
        return _mapper.ToSubscription(subscription);
    }

    public Task UnsubscribeAsync(Guid subscriptionId) => _subscriptionService.UnsubscribeAsync(subscriptionId);

    public Task UnsubscribeByTargetAsync(SubscriptionTargetType targetType, Guid targetId) =>
        _subscriptionService.UnsubscribeByTargetAsync(targetType, targetId);

    public async Task<Subscription?> GetSubscriptionAsync(SubscriptionTargetType targetType, Guid targetId)
    {
        var subscription = await _subscriptionService.GetSubscriptionAsync(targetType, targetId);
        return subscription == null ? null : _mapper.ToSubscription(subscription);
    }

    public async Task<Subscription?> GetByIdAsync(Guid subscriptionId)
    {
        var subscription = await _subscriptionService.GetByIdAsync(subscriptionId);
        return subscription == null ? null : _mapper.ToSubscription(subscription);
    }
}
