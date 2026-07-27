using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Community.Features.Achievements;
using DM.Domain.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using EntityAchievementCategory = DM.Infrastructure.Persistence.Entities.Community.AchievementCategory;
using EntityAchievementType = DM.Infrastructure.Persistence.Entities.Community.AchievementType;
using EntityUserAchievement = DM.Infrastructure.Persistence.Entities.Community.UserAchievement;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class AchievementRepository : IAchievementRepository
{
    private readonly DmDbContext _db;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;

    public AchievementRepository(DmDbContext db, IMapper mapper, IGuidFactory guidFactory)
    {
        _db = db;
        _mapper = mapper;
        _guidFactory = guidFactory;
    }

    // ---- Categories ----

    public async Task<IReadOnlyCollection<AchievementCategory>> GetCategoriesAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = _db.AchievementCategories.AsNoTracking();
        if (!includeInactive) query = query.Where(c => c.IsActive);
        return await query
            .OrderBy(c => c.SortOrder)
            .ProjectTo<AchievementCategory>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<AchievementCategory?> GetCategoryAsync(Guid id, CancellationToken ct = default) =>
        await _db.AchievementCategories
            .AsNoTracking()
            .Where(c => c.AchievementCategoryId == id)
            .ProjectTo<AchievementCategory>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

    public async Task<AchievementCategory> UpdateCategoryAsync(UpdateAchievementCategory update, CancellationToken ct = default)
    {
        var entity = await _db.AchievementCategories.FirstAsync(c => c.AchievementCategoryId == update.Id, ct);
        if (update.Title != null) entity.Title = update.Title;
        if (update.Description != null) entity.Description = update.Description;
        if (update.IconName != null) entity.IconName = update.IconName;
        if (update.SortOrder.HasValue) entity.SortOrder = update.SortOrder.Value;
        if (update.IsActive.HasValue) entity.IsActive = update.IsActive.Value;
        await _db.SaveChangesAsync(ct);
        return _mapper.Map<AchievementCategory>(entity);
    }

    // ---- Tiers ----

    public async Task<IReadOnlyCollection<AchievementType>> GetTypesAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = _db.AchievementTypes
            .AsNoTracking()
            .Include(t => t.Category)
            .AsQueryable();
        if (!includeInactive) query = query.Where(t => t.Category.IsActive);
        return await query
            .OrderBy(t => t.Category.SortOrder)
            .ThenBy(t => t.Threshold)
            .ProjectTo<AchievementType>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<AchievementType?> GetTypeAsync(Guid id, CancellationToken ct = default) =>
        await _db.AchievementTypes
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.AchievementTypeId == id)
            .ProjectTo<AchievementType>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

    public async Task<AchievementType?> GetTypeByCodeAsync(string code, CancellationToken ct = default) =>
        await _db.AchievementTypes
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.Code == code)
            .ProjectTo<AchievementType>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

    public async Task<AchievementType> CreateTypeAsync(CreateAchievementType create, CancellationToken ct = default)
    {
        var entity = new EntityAchievementType
        {
            AchievementTypeId = _guidFactory.Create(),
            Code = create.Code,
            Title = create.Title,
            Threshold = create.Threshold,
            Tier = create.Tier,
            AchievementCategoryId = create.AchievementCategoryId,
        };
        _db.AchievementTypes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return (await GetTypeAsync(entity.AchievementTypeId, ct))!;
    }

    public async Task<AchievementType> UpdateTypeAsync(UpdateAchievementType update, CancellationToken ct = default)
    {
        var entity = await _db.AchievementTypes.FirstAsync(t => t.AchievementTypeId == update.Id, ct);
        if (update.Title != null) entity.Title = update.Title;
        if (update.Threshold.HasValue) entity.Threshold = update.Threshold.Value;
        if (update.Tier.HasValue) entity.Tier = update.Tier;
        await _db.SaveChangesAsync(ct);
        return (await GetTypeAsync(entity.AchievementTypeId, ct))!;
    }

    public async Task DeleteTypeAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.AchievementTypes.FirstOrDefaultAsync(t => t.AchievementTypeId == id, ct);
        if (entity == null) return;
        _db.AchievementTypes.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ---- Earnings ----

    public async Task<IReadOnlyCollection<UserAchievement>> GetUserAchievementsAsync(Guid userId, CancellationToken ct = default) =>
        await _db.UserAchievements
            .AsNoTracking()
            .Include(a => a.AchievementType).ThenInclude(t => t.Category)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.EarnedUtc)
            .ProjectTo<UserAchievement>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

    public async Task<bool> TryGrantAsync(Guid userId, Guid achievementTypeId, DateTimeOffset earnedUtc, CancellationToken ct = default)
    {
        var entity = new EntityUserAchievement
        {
            UserAchievementId = _guidFactory.Create(),
            UserId = userId,
            AchievementTypeId = achievementTypeId,
            EarnedUtc = earnedUtc,
            IsRemoved = false,
        };
        _db.UserAchievements.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // UNIQUE(UserId, AchievementTypeId) — a parallel
            // evaluator has already granted it. Roll back the tracker and report
            // "already there". This is a normal situation, not an error.
            _db.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }
}
