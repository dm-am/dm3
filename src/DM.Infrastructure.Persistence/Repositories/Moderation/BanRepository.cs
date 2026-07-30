using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Moderation.Features.Warnings;
using Microsoft.EntityFrameworkCore;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
internal class BanRepository : IBanRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public BanRepository(DmDbContext dbContext, IMapper mapper, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ban>> GetUserBans(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Bans
            .Where(b => b.TargetUserId == userId && !b.IsRemoved)
            .OrderByDescending(b => b.StartedUtc)
            .ProjectTo<Ban>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ban?> GetActiveBan(Guid userId, CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;
        return await _dbContext.Bans
            .Where(b => b.TargetUserId == userId && !b.IsRemoved && b.StartedUtc <= now && b.EndedUtc > now)
            .OrderByDescending(b => b.EndedUtc)
            .ProjectTo<Ban>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ban?> Get(Guid banId, CancellationToken ct = default)
    {
        return await _dbContext.Bans
            .Where(b => b.BanId == banId)
            .ProjectTo<Ban>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ban> Create(CreateBanEntity entity, CancellationToken ct = default)
    {
        var ban = new DbBan
        {
            BanId = entity.BanId,
            TargetUserId = entity.TargetUserId,
            AuthorId = entity.AuthorId,
            StartedUtc = entity.StartedUtc,
            EndedUtc = entity.EndedUtc,
            Comment = entity.Comment,
            AccessRestrictionPolicy = entity.AccessRestrictionPolicy,
            IsVoluntary = entity.IsVoluntary,
            IsRemoved = false
        };

        _dbContext.Bans.Add(ban);
        await _dbContext.SaveChangesAsync(ct);

        return await Get(ban.BanId, ct) ?? throw new InvalidOperationException("Failed to retrieve created ban");
    }

    /// <inheritdoc />
    public async Task Remove(Guid banId, Guid liftedByUserId, DateTimeOffset liftedUtc, string? reason,
        CancellationToken ct = default)
    {
        var ban = await _dbContext.Bans.FindAsync(new object[] { banId }, ct);
        if (ban != null)
        {
            ban.IsRemoved = true;
            // Lifting a ban used to leave no trace at all: the reason reached the
            // service and was dropped, so nobody could tell who had lifted what.
            ban.LiftedByUserId = liftedByUserId;
            ban.LiftedUtc = liftedUtc;
            ban.LiftReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ban>> GetAllActiveBans(CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;
        return await _dbContext.Bans
            .Where(b => !b.IsRemoved && b.StartedUtc <= now && b.EndedUtc > now)
            .OrderByDescending(b => b.StartedUtc)
            .ProjectTo<Ban>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Ban> Bans, int TotalCount)> GetBanHistory(
        int skip, int take, CancellationToken ct = default)
    {
        var totalCount = await _dbContext.Bans.CountAsync(ct);
        var bans = await _dbContext.Bans
            .OrderByDescending(b => b.StartedUtc)
            .Skip(skip)
            .Take(take)
            .ProjectTo<Ban>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return (bans, totalCount);
    }
}
