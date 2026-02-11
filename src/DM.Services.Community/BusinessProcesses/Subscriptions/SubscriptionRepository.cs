using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Subscriptions;

/// <inheritdoc />
internal class SubscriptionRepository : ISubscriptionRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public SubscriptionRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetUserSubscriptions(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.SubscriberId == userId)
            .OrderByDescending(s => s.CreatedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetUserSubscriptions(Guid userId, SubscriptionTargetType targetType, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.SubscriberId == userId && s.TargetType == targetType)
            .OrderByDescending(s => s.CreatedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Subscription?> Get(Guid subscriptionId, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetByTarget(SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.TargetType == targetType && s.TargetId == targetId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subscription>> GetByTargetWithSettings(SubscriptionTargetType targetType, Guid targetId, SubscriptionSettings requiredSettings, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .Where(s => s.TargetType == targetType && s.TargetId == targetId)
            .Where(s => (s.Settings & requiredSettings) != 0)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Subscription?> Find(Guid userId, SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        return await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.SubscriberId == userId && s.TargetType == targetType && s.TargetId == targetId, ct);
    }

    /// <inheritdoc />
    public async Task<Subscription> Create(Subscription subscription, CancellationToken ct = default)
    {
        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    /// <inheritdoc />
    public async Task<Subscription> Update(Subscription subscription, CancellationToken ct = default)
    {
        _dbContext.Subscriptions.Update(subscription);
        await _dbContext.SaveChangesAsync(ct);
        return subscription;
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
    public async Task DeleteParticipationSubscriptions(Guid userId, SubscriptionTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        var subscriptions = await _dbContext.Subscriptions
            .Where(s => s.SubscriberId == userId && s.TargetType == targetType && s.TargetId == targetId && s.Source == SubscriptionSource.Participation)
            .ToListAsync(ct);

        if (subscriptions.Any())
        {
            _dbContext.Subscriptions.RemoveRange(subscriptions);
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
