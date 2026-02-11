using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Administration;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Moderation.Warnings;

/// <inheritdoc />
internal class BanRepository : IBanRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public BanRepository(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ban>> GetUserBans(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Bans
            .Include(b => b.Moderator)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.StartedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ban?> GetActiveBan(Guid userId, CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;
        return await _dbContext.Bans
            .Include(b => b.Moderator)
            .Where(b => b.UserId == userId && b.EndedUtc > now)
            .OrderByDescending(b => b.EndedUtc)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ban?> Get(Guid banId, CancellationToken ct = default)
    {
        return await _dbContext.Bans
            .Include(b => b.User)
            .Include(b => b.Moderator)
            .FirstOrDefaultAsync(b => b.BanId == banId, ct);
    }

    /// <inheritdoc />
    public async Task<Ban> Create(Ban ban, CancellationToken ct = default)
    {
        _dbContext.Bans.Add(ban);
        await _dbContext.SaveChangesAsync(ct);
        return ban;
    }

    /// <inheritdoc />
    public async Task Remove(Guid banId, CancellationToken ct = default)
    {
        var ban = await _dbContext.Bans.FindAsync(new object[] { banId }, ct);
        if (ban != null)
        {
            ban.IsRemoved = true;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsUserBanned(Guid userId, CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;
        return await _dbContext.Bans
            .AnyAsync(b => b.UserId == userId && b.EndedUtc > now, ct);
    }
}
