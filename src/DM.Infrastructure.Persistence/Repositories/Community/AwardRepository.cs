using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Community.Features.Awards;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using EntityAwardType = DM.Infrastructure.Persistence.Entities.Community.AwardType;
using EntityContestSeries = DM.Infrastructure.Persistence.Entities.Community.ContestSeries;
using EntityUserAward = DM.Infrastructure.Persistence.Entities.Community.UserAward;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class AwardRepository : IAwardRepository
{
    private readonly DmDbContext _db;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _clock;

    public AwardRepository(DmDbContext db, IMapper mapper, IGuidFactory guidFactory, IDateTimeProvider clock)
    {
        _db = db;
        _mapper = mapper;
        _guidFactory = guidFactory;
        _clock = clock;
    }

    // ---- AwardType ----

    public async Task<IReadOnlyCollection<AwardType>> GetTypesAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = _db.AwardTypes.AsNoTracking();
        if (!includeInactive) query = query.Where(t => t.IsActive);
        return await query
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Title)
            .ProjectTo<AwardType>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<AwardType?> GetTypeAsync(Guid id, CancellationToken ct = default) =>
        await _db.AwardTypes
            .AsNoTracking()
            .Where(t => t.AwardTypeId == id)
            .ProjectTo<AwardType>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

    public async Task<AwardType?> GetTypeByCodeAsync(string code, CancellationToken ct = default) =>
        await _db.AwardTypes
            .AsNoTracking()
            .Where(t => t.Code == code)
            .ProjectTo<AwardType>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

    public async Task<AwardType> CreateTypeAsync(CreateAwardType create, CancellationToken ct = default)
    {
        var entity = new EntityAwardType
        {
            AwardTypeId = _guidFactory.Create(),
            Code = create.Code,
            Title = create.Title,
            Description = create.Description,
            IconName = create.IconName,
            Tier = create.Tier,
            SortOrder = create.SortOrder,
            IsActive = true,
        };
        _db.AwardTypes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return _mapper.Map<AwardType>(entity);
    }

    public async Task<AwardType> UpdateTypeAsync(UpdateAwardType update, CancellationToken ct = default)
    {
        var entity = await _db.AwardTypes.FirstAsync(t => t.AwardTypeId == update.Id, ct);
        if (update.Title != null) entity.Title = update.Title;
        if (update.Description != null) entity.Description = update.Description;
        if (update.IconName != null) entity.IconName = update.IconName;
        if (update.Tier.HasValue) entity.Tier = update.Tier;
        if (update.SortOrder.HasValue) entity.SortOrder = update.SortOrder.Value;
        if (update.IsActive.HasValue) entity.IsActive = update.IsActive.Value;
        await _db.SaveChangesAsync(ct);
        return _mapper.Map<AwardType>(entity);
    }

    // ---- ContestSeries ----

    public async Task<IReadOnlyCollection<ContestSeries>> GetSeriesAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = _db.ContestSeries.AsNoTracking();
        if (!includeInactive) query = query.Where(s => s.IsActive);
        return await query
            .OrderByDescending(s => s.Year)
            .ThenBy(s => s.ContestType)
            .ThenByDescending(s => s.Number)
            .ProjectTo<ContestSeries>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<ContestSeries?> GetSeriesAsync(Guid id, CancellationToken ct = default) =>
        await _db.ContestSeries
            .AsNoTracking()
            .Where(s => s.ContestSeriesId == id)
            .ProjectTo<ContestSeries>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

    public async Task<ContestSeries> CreateSeriesAsync(CreateContestSeries create, CancellationToken ct = default)
    {
        var entity = new EntityContestSeries
        {
            ContestSeriesId = _guidFactory.Create(),
            ContestType = create.ContestType,
            Number = create.Number,
            Year = create.Year,
            TopicUrl = string.IsNullOrWhiteSpace(create.TopicUrl) ? null : create.TopicUrl.Trim(),
            IsActive = true,
        };
        _db.ContestSeries.Add(entity);
        await _db.SaveChangesAsync(ct);
        return _mapper.Map<ContestSeries>(entity);
    }

    public async Task<ContestSeries> UpdateSeriesAsync(UpdateContestSeries update, CancellationToken ct = default)
    {
        var entity = await _db.ContestSeries.FirstAsync(s => s.ContestSeriesId == update.Id, ct);
        if (update.ContestType.HasValue) entity.ContestType = update.ContestType.Value;
        if (update.Number.HasValue) entity.Number = update.Number.Value;
        if (update.Year.HasValue) entity.Year = update.Year.Value;
        if (update.TopicUrl != null) entity.TopicUrl = string.IsNullOrWhiteSpace(update.TopicUrl) ? null : update.TopicUrl.Trim();
        if (update.IsActive.HasValue) entity.IsActive = update.IsActive.Value;
        await _db.SaveChangesAsync(ct);
        return _mapper.Map<ContestSeries>(entity);
    }

    // ---- UserAward ----

    public async Task<IReadOnlyCollection<UserAward>> GetUserAwardsAsync(Guid userId, CancellationToken ct = default) =>
        // Sort oldest first — the older the award, the earlier it appears. Ties
        // on the same date (a placement + a special award from the same contest)
        // break by award type (SortOrder ASC: 1st → 2nd → 3rd → Народное →
        // Критик → Угадайка). Non-contest honours dated to the user's early
        // years (the honorary goblin) therefore lead the list.
        await _db.UserAwards
            .AsNoTracking()
            .Where(a => a.UserId == userId && !a.IsRemoved)
            .OrderBy(a => a.AwardedUtc)
            .ThenBy(a => a.AwardType.SortOrder)
            .ProjectTo<UserAward>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

    // Ordered by award type first: a contest reads as a podium (1st, 2nd, 3rd,
    // then the special awards), not as a grant log.
    public async Task<IReadOnlyCollection<UserAward>> GetSeriesAwardsAsync(Guid seriesId, CancellationToken ct = default) =>
        await _db.UserAwards
            .AsNoTracking()
            .Where(a => a.ContestSeriesId == seriesId && !a.IsRemoved)
            .OrderBy(a => a.AwardType.SortOrder)
            .ThenBy(a => a.AwardedUtc)
            .ProjectTo<UserAward>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

    public async Task<UserAward?> GetAsync(Guid id, CancellationToken ct = default) =>
        await _db.UserAwards
            .AsNoTracking()
            .Where(a => a.UserAwardId == id)
            .ProjectTo<UserAward>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

    public async Task<UserAward> CreateAsync(CreateUserAward create, Guid awardedByUserId, CancellationToken ct = default)
    {
        var entity = new EntityUserAward
        {
            UserAwardId = _guidFactory.Create(),
            UserId = create.UserId,
            AwardTypeId = create.AwardTypeId,
            ContestSeriesId = create.ContestSeriesId,
            WorkUrl = create.WorkUrl,
            AwardedUtc = _clock.Now,
            AwardedByUserId = awardedByUserId,
            IsRemoved = false,
        };
        _db.UserAwards.Add(entity);
        await _db.SaveChangesAsync(ct);

        // Re-read via the projection to pull in the navigations (Type, Series, User).
        return (await GetAsync(entity.UserAwardId, ct))!;
    }

    public async Task RevokeAsync(Guid id, Guid revokedByUserId, CancellationToken ct = default)
    {
        var entity = await _db.UserAwards.FirstAsync(a => a.UserAwardId == id, ct);
        SoftDelete.Mark(entity, revokedByUserId, _clock.Now);
        await _db.SaveChangesAsync(ct);
    }
}
