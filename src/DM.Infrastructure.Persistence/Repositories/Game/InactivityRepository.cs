using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Inactivity;
using Microsoft.EntityFrameworkCore;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class InactivityRepository : IInactivityRepository
{
    private readonly DmDbContext _dbContext;

    public InactivityRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Each condition is read by the selection and repeated by its write, so a game that
    // posted or restarted in between is left alone. Removed games are out by the global filter.

    private static Expression<Func<DbGame, bool>> SilentAndUnwarned(DateTimeOffset threshold) => g =>
        g.Status == ModuleStatus.Active &&
        g.InactivityWarningUtc == null &&
        ((g.LastPostCreatedUtc == null && g.ActivatedUtc < threshold) ||
         (g.LastPostCreatedUtc != null && g.LastPostCreatedUtc < threshold));

    private static readonly Expression<Func<DbGame, bool>> WarnedAndSilent = g =>
        g.Status == ModuleStatus.Active &&
        g.InactivityWarningUtc != null &&
        (g.LastPostCreatedUtc == null || g.LastPostCreatedUtc < g.InactivityWarningUtc);

    private static readonly Expression<Func<DbGame, bool>> FrozenUnwarned = g =>
        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Frozen && g.ClosureWarningUtc == null;

    private static readonly Expression<Func<DbGame, bool>> FrozenWarned = g =>
        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Frozen && g.ClosureWarningUtc != null;

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetInactiveGamesToWarn(
        TimeSpan inactivityThreshold, DateTimeOffset now, CancellationToken ct = default)
    {
        var threshold = now - inactivityThreshold;

        return await _dbContext.Games
            .Where(SilentAndUnwarned(threshold))
            .Select(g => g.GameId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetWarnedGamesToFreeze(
        TimeSpan warningGracePeriod, DateTimeOffset now, CancellationToken ct = default)
    {
        var threshold = now - warningGracePeriod;

        return await _dbContext.Games
            .Where(WarnedAndSilent)
            .Where(g => g.InactivityWarningUtc < threshold)
            .Select(g => g.GameId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> GetFrozenGamesToWarn(
        TimeSpan frozenThreshold, DateTimeOffset now, CancellationToken ct = default)
    {
        var threshold = now - frozenThreshold;

        return await _dbContext.Games
            .Where(FrozenUnwarned)
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
            .Where(FrozenWarned)
            .Where(g => g.ClosureWarningUtc < threshold)
            .Select(g => g.GameId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The same state the selection above asked for, repeated in the WHERE clause
    /// of the write. The batch is read once and worked through game by game, so
    /// between the two a master may have posted or restarted the game; without the
    /// predicate the stamp lands anyway and starts the week that ends in a freeze.
    /// The row count is what tells the caller whether that happened.
    /// </remarks>
    public async Task<bool> SetInactivityWarning(
        Guid gameId, TimeSpan inactivityThreshold, DateTimeOffset now, CancellationToken ct = default)
    {
        // The silence itself, measured back from the same moment the selection used:
        // a post landing between the two clears the stamp, so "still unwarned" alone
        // would warn a game that had just spoken.
        var threshold = now - inactivityThreshold;

        var written = await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .Where(SilentAndUnwarned(threshold))
            .ExecuteUpdateAsync(g => g
                .SetProperty(x => x.InactivityWarningUtc, now), ct);

        return written == 1;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <inheritdoc cref="SetInactivityWarning" path="/remarks" />
    /// A post arriving after the warning is the whole question this pass answers,
    /// and a post clears the warning stamp as it lands, so both halves of it are
    /// here: the game is frozen only while it is still warned and still silent.
    /// </remarks>
    public async Task<bool> FreezeGame(Guid gameId, DateTimeOffset closedUtc, CancellationToken ct = default)
    {
        var written = await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .Where(WarnedAndSilent)
            .ExecuteUpdateAsync(g => g
                .SetProperty(x => x.Status, ModuleStatus.Closed)
                .SetProperty(x => x.ClosedReason, ClosedReason.Frozen)
                .SetProperty(x => x.ClosedUtc, closedUtc)
                .SetProperty(x => x.InactivityWarningUtc, (DateTimeOffset?)null), ct);

        return written == 1;
    }

    /// <inheritdoc />
    /// <remarks><inheritdoc cref="SetInactivityWarning" path="/remarks" /></remarks>
    public async Task<bool> SetClosureWarning(
        Guid gameId, DateTimeOffset warningUtc, CancellationToken ct = default)
    {
        var written = await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .Where(FrozenUnwarned)
            .ExecuteUpdateAsync(g => g
                .SetProperty(x => x.ClosureWarningUtc, warningUtc), ct);

        return written == 1;
    }

    /// <inheritdoc />
    /// <remarks><inheritdoc cref="SetInactivityWarning" path="/remarks" /></remarks>
    public async Task<bool> CloseGame(Guid gameId, CancellationToken ct = default)
    {
        var written = await _dbContext.Games
            .Where(g => g.GameId == gameId)
            .Where(FrozenWarned)
            .ExecuteUpdateAsync(g => g
                .SetProperty(x => x.ClosedReason, ClosedReason.None)
                .SetProperty(x => x.ClosureWarningUtc, (DateTimeOffset?)null), ct);

        return written == 1;
    }
}
