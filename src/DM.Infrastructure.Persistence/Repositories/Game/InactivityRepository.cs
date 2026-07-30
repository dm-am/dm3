using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Inactivity;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class InactivityRepository : IInactivityRepository
{
    private readonly DmDbContext _dbContext;

    public InactivityRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetInactiveGamesToWarn(
        TimeSpan inactivityThreshold, DateTimeOffset now, CancellationToken ct = default)
    {
        var threshold = now - inactivityThreshold;

        return await _dbContext.Games
            .Where(g => !g.IsRemoved)
            .Where(g => g.Status == ModuleStatus.Active)
            .Where(g => g.InactivityWarningUtc == null)
            // Either no posts at all (check activation date) or last post is old
            .Where(g => (g.LastPostCreatedUtc == null && g.ActivatedUtc < threshold) ||
                        (g.LastPostCreatedUtc != null && g.LastPostCreatedUtc < threshold))
            .Select(g => g.GameId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetWarnedGamesToFreeze(
        TimeSpan warningGracePeriod, DateTimeOffset now, CancellationToken ct = default)
    {
        var threshold = now - warningGracePeriod;

        return await _dbContext.Games
            .Where(g => !g.IsRemoved)
            .Where(g => g.Status == ModuleStatus.Active)
            .Where(g => g.InactivityWarningUtc != null)
            .Where(g => g.InactivityWarningUtc < threshold)
            // Check that no new posts were created after warning
            .Where(g => g.LastPostCreatedUtc == null || g.LastPostCreatedUtc < g.InactivityWarningUtc)
            .Select(g => g.GameId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetFrozenGamesToWarn(
        TimeSpan frozenThreshold, DateTimeOffset now, CancellationToken ct = default)
    {
        var threshold = now - frozenThreshold;

        return await _dbContext.Games
            .Where(g => !g.IsRemoved)
            .Where(g => g.Status == ModuleStatus.Closed)
            .Where(g => g.ClosedReason == ClosedReason.Frozen)
            .Where(g => g.ClosureWarningUtc == null)
            .Where(g => g.ClosedUtc != null && g.ClosedUtc < threshold)
            .Select(g => g.GameId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetWarnedFrozenGamesToClose(
        TimeSpan warningGracePeriod, DateTimeOffset now, CancellationToken ct = default)
    {
        var threshold = now - warningGracePeriod;

        return await _dbContext.Games
            .Where(g => !g.IsRemoved)
            .Where(g => g.Status == ModuleStatus.Closed)
            .Where(g => g.ClosedReason == ClosedReason.Frozen)
            .Where(g => g.ClosureWarningUtc != null)
            .Where(g => g.ClosureWarningUtc < threshold)
            .Select(g => g.GameId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetInactivityWarning(Guid gameId, DateTimeOffset warningUtc, CancellationToken ct = default)
    {
        await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .ExecuteUpdateAsync(g => g
                .SetProperty(x => x.InactivityWarningUtc, warningUtc), ct);
    }

    /// <inheritdoc />
    public async Task FreezeGame(Guid gameId, DateTimeOffset closedUtc, CancellationToken ct = default)
    {
        await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .ExecuteUpdateAsync(g => g
                .SetProperty(x => x.Status, ModuleStatus.Closed)
                .SetProperty(x => x.ClosedReason, ClosedReason.Frozen)
                .SetProperty(x => x.ClosedUtc, closedUtc)
                .SetProperty(x => x.InactivityWarningUtc, (DateTimeOffset?)null), ct);
    }

    /// <inheritdoc />
    public async Task SetClosureWarning(Guid gameId, DateTimeOffset warningUtc, CancellationToken ct = default)
    {
        await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .ExecuteUpdateAsync(g => g
                .SetProperty(x => x.ClosureWarningUtc, warningUtc), ct);
    }

    /// <inheritdoc />
    public async Task CloseGame(Guid gameId, CancellationToken ct = default)
    {
        await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .ExecuteUpdateAsync(g => g
                .SetProperty(x => x.ClosedReason, ClosedReason.None)
                .SetProperty(x => x.ClosureWarningUtc, (DateTimeOffset?)null), ct);
    }

    /// <inheritdoc />
    public async Task<string?> GetGameTitle(Guid gameId, CancellationToken ct = default)
    {
        return await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .Select(g => g.Title)
            .FirstOrDefaultAsync(ct);
    }
}
