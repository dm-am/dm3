using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Subscriptions;
using Microsoft.EntityFrameworkCore;
using SubscriptionEntity = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;

namespace DM.Infrastructure.Persistence.Shared.Subscriptions;

/// <inheritdoc />
internal class SubscriptionRepository : ISubscriptionRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public SubscriptionRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetUserSubscriptions(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.SubscriberId == userId)
            .OrderByDescending(s => s.CreatedUtc)
            .ProjectTo<Subscription>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetUserSubscriptions(Guid userId, SubscriptionTargetType targetType, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.SubscriberId == userId && s.TargetType == targetType)
            .OrderByDescending(s => s.CreatedUtc)
            .ProjectTo<Subscription>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Subscription?> Get(Guid subscriptionId, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.SubscriptionId == subscriptionId)
            .ProjectTo<Subscription>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetByTarget(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.TargetType == targetType && s.TargetId == targetId)
            .ProjectTo<Subscription>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetByTargetWithSettings(SubscriptionTargetType targetType, Guid targetId, SubscriptionSettings requiredSettings, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.TargetType == targetType && s.TargetId == targetId)
            .Where(s => (s.Settings & requiredSettings) != 0)
            .ProjectTo<Subscription>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Subscription?> Find(Guid userId, SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.SubscriberId == userId && s.TargetType == targetType && s.TargetId == targetId)
            .ProjectTo<Subscription>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Subscription> Create(CreateSubscription subscription, CancellationToken ct = default)
    {
        var entity = new SubscriptionEntity
        {
            SubscriptionId = subscription.SubscriptionId,
            SubscriberId = subscription.SubscriberId,
            TargetType = subscription.TargetType,
            TargetId = subscription.TargetId,
            Settings = subscription.Settings,
            CreatedUtc = subscription.CreatedUtc
        };

        _dbContext.Subscriptions.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        return (await Get(entity.SubscriptionId, ct))!;
    }

    /// <inheritdoc />
    public async Task<Subscription> Update(UpdateSubscription subscription, CancellationToken ct = default)
    {
        var entity = await _dbContext.Subscriptions.FindAsync(new object[] { subscription.SubscriptionId }, ct);
        if (entity == null)
        {
            throw new InvalidOperationException($"Subscription {subscription.SubscriptionId} not found");
        }

        entity.Settings = subscription.Settings;
        entity.UpdatedUtc = subscription.UpdatedUtc;

        await _dbContext.SaveChangesAsync(ct);

        return (await Get(entity.SubscriptionId, ct))!;
    }

    /// <inheritdoc />
    public async Task Delete(Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await _dbContext.Subscriptions.FindAsync(new object[] { subscriptionId }, ct);
        if (subscription != null)
        {
            _dbContext.Subscriptions.Remove(subscription);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task Delete(Guid userId, SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.SubscriberId == userId && s.TargetType == targetType && s.TargetId == targetId, ct);
        if (subscription != null)
        {
            _dbContext.Subscriptions.Remove(subscription);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetTargetSubscriberIds(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.TargetType == targetType && s.TargetId == targetId)
            .Select(s => s.SubscriberId)
            .Distinct()
            .ToListAsync(ct);
    }
}
