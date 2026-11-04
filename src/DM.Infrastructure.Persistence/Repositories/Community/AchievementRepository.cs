using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Achievements;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbAchievementType = DM.Infrastructure.Persistence.Entities.Community.AchievementType;
using DbUserAchievement = DM.Infrastructure.Persistence.Entities.Community.UserAchievement;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class AchievementRepository : IAchievementRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;

    public AchievementRepository(DmDbContext dbContext, IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
    }

    public async Task<IReadOnlyCollection<AchievementCategory>> GetCategoriesAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = _dbContext.AchievementCategories.AsNoTracking();
        if (!includeInactive) query = query.Where(c => c.IsActive);
        return await query
            .OrderBy(c => c.SortOrder)
            .ProjectToCategory()
            .ToListAsync(ct);
    }

    public async Task<AchievementCategory?> GetCategoryAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.AchievementCategories
            .AsNoTracking()
            .Where(c => c.AchievementCategoryId == id)
            .ProjectToCategory()
            .FirstOrDefaultAsync(ct);

    public async Task<AchievementCategory> UpdateCategoryAsync(UpdateAchievementCategory update, CancellationToken ct = default)
    {
        var entity = await _dbContext.AchievementCategories.FirstAsync(c => c.AchievementCategoryId == update.Id, ct);
        if (update.Title != null) entity.Title = update.Title;
        if (update.Description != null) entity.Description = update.Description;
        if (update.IconName != null) entity.IconName = update.IconName;
        if (update.SortOrder.HasValue) entity.SortOrder = update.SortOrder.Value;
        if (update.IsActive.HasValue) entity.IsActive = update.IsActive.Value;
        await _dbContext.SaveChangesAsync(ct);
        return entity.ToCategory();
    }

    public async Task<IReadOnlyCollection<AchievementType>> GetTypesAsync(bool includeInactive, CancellationToken ct = default)
    {
        // No Include: the projection below reads the category through the
        // mapping, and the two filters over it join it anyway. Left in, it was a
        // line that changed neither the SQL nor the result - which is the worst
        // kind, because the next reader takes it for the thing that makes the
        // category available.
        var query = _dbContext.AchievementTypes.AsNoTracking();
        if (!includeInactive) query = query.Where(t => t.Category.IsActive);
        return await query
            .OrderBy(t => t.Category.SortOrder)
            .ThenBy(t => t.Threshold)
            .ProjectToType()
            .ToListAsync(ct);
    }

    public async Task<AchievementType?> GetTypeAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.AchievementTypes
            .AsNoTracking()
            .Where(t => t.AchievementTypeId == id)
            .ProjectToType()
            .FirstOrDefaultAsync(ct);

    public async Task<AchievementType?> GetTypeByCodeAsync(string code, CancellationToken ct = default) =>
        await _dbContext.AchievementTypes
            .AsNoTracking()
            .Where(t => t.Code == code)
            .ProjectToType()
            .FirstOrDefaultAsync(ct);

    public async Task<AchievementType> CreateTypeAsync(CreateAchievementType create, CancellationToken ct = default)
    {
        var entity = new DbAchievementType
        {
            AchievementTypeId = _guidFactory.Create(),
            Code = create.Code,
            Title = create.Title,
            Threshold = create.Threshold,
            Tier = create.Tier,
            AchievementCategoryId = create.AchievementCategoryId,
        };
        _dbContext.AchievementTypes.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        return (await GetTypeAsync(entity.AchievementTypeId, ct))!;
    }

    public async Task<AchievementType> UpdateTypeAsync(UpdateAchievementType update, CancellationToken ct = default)
    {
        var entity = await _dbContext.AchievementTypes.FirstAsync(t => t.AchievementTypeId == update.Id, ct);
        if (update.Title != null) entity.Title = update.Title;
        if (update.Threshold.HasValue) entity.Threshold = update.Threshold.Value;
        if (update.Tier.HasValue) entity.Tier = update.Tier;
        await _dbContext.SaveChangesAsync(ct);
        return (await GetTypeAsync(entity.AchievementTypeId, ct))!;
    }

    public async Task DeleteTypeAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbContext.AchievementTypes.FirstOrDefaultAsync(t => t.AchievementTypeId == id, ct);
        if (entity == null) return;
        _dbContext.AchievementTypes.Remove(entity);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<UserAchievement>> GetUserAchievementsAsync(Guid userId, CancellationToken ct = default) =>
        await _dbContext.UserAchievements
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.EarnedUtc)
            .ProjectToUserAchievement()
            .ToListAsync(ct);

    public async Task<bool> TryGrantAsync(Guid userId, Guid achievementTypeId, DateTimeOffset earnedUtc, CancellationToken ct = default)
    {
        var entity = new DbUserAchievement
        {
            UserAchievementId = _guidFactory.Create(),
            UserId = userId,
            AchievementTypeId = achievementTypeId,
            EarnedUtc = earnedUtc,
            IsRemoved = false,
        };
        _dbContext.UserAchievements.Add(entity);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
        {
            // UNIQUE(UserId, AchievementTypeId) — a parallel
            // evaluator has already granted it. Roll back the tracker and report
            // "already there". This is a normal situation, not an error.
            _dbContext.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }
}
