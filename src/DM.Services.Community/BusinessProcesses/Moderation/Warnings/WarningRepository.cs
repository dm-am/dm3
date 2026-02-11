using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Administration;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Moderation.Warnings;

/// <inheritdoc />
internal class WarningRepository : IWarningRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public WarningRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Warning>> GetUserWarnings(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Warnings
            .Include(w => w.Moderator)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Warning?> Get(Guid warningId, CancellationToken ct = default)
    {
        return await _dbContext.Warnings
            .Include(w => w.User)
            .Include(w => w.Moderator)
            .FirstOrDefaultAsync(w => w.WarningId == warningId, ct);
    }

    /// <inheritdoc />
    public async Task<Warning> Create(Warning warning, CancellationToken ct = default)
    {
        _dbContext.Warnings.Add(warning);
        await _dbContext.SaveChangesAsync(ct);
        return warning;
    }

    /// <inheritdoc />
    public async Task Remove(Guid warningId, CancellationToken ct = default)
    {
        var warning = await _dbContext.Warnings.FindAsync(new object[] { warningId }, ct);
        if (warning != null)
        {
            warning.IsRemoved = true;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<int> GetUserWarningPoints(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Warnings
            .Where(w => w.UserId == userId)
            .SumAsync(w => w.Points, ct);
    }
}
