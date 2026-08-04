using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Popularity;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class GamePopularityRepository : IGamePopularityRepository
{
    /// <summary>Games per write batch, so one pass cannot hold the store for minutes.</summary>
    private const int BatchSize = 100;

    private readonly DmDbContext _dbContext;

    /// <inheritdoc cref="GamePopularityRepository" />
    public GamePopularityRepository(DmDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Guid>> GetScorableGameIds(CancellationToken cancellationToken = default) =>
        await _dbContext.Games
            .TagWith("DM.Game.ScorableGames")
            .Where(g => !g.IsRemoved && g.Status != ModuleStatus.Draft)
            .Select(g => g.GameId)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CountActivePlayers(
        IReadOnlyCollection<Guid> gameIds,
        DateTimeOffset activeSince,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Characters
            .TagWith("DM.Game.ActivePlayerCounts")
            .Where(c => gameIds.Contains(c.GameId) &&
                        c.Status == CharacterStatus.Active &&
                        !c.IsNpc &&
                        c.AuthorId.HasValue &&
                        c.Author != null &&
                        c.Author.LastActivityUtc.HasValue &&
                        c.Author.LastActivityUtc.Value > activeSince)
            .GroupBy(c => c.GameId)
            .Select(g => new
            {
                GameId = g.Key,
                Count = g.Select(c => c.AuthorId!.Value).Distinct().Count(),
            })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CountActiveReaders(
        IReadOnlyCollection<Guid> gameIds,
        DateTimeOffset activeSince,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Subscriptions
            .TagWith("DM.Game.ActiveReaderCounts")
            .Where(s => s.TargetType == SubscriptionTargetType.Game &&
                        gameIds.Contains(s.TargetId) &&
                        s.Subscriber.LastActivityUtc.HasValue &&
                        s.Subscriber.LastActivityUtc.Value > activeSince)
            .GroupBy(s => s.TargetId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, cancellationToken);

    /// <inheritdoc />
    public async Task<int> ApplyScores(
        IReadOnlyDictionary<Guid, int> scores,
        DateTimeOffset calculatedUtc,
        CancellationToken cancellationToken = default)
    {
        var updated = 0;

        foreach (var batch in scores.Keys.Chunk(BatchSize))
        {
            var games = await _dbContext.Games
                .TagWith("DM.Game.ApplyPopularityScores")
                .Where(g => batch.Contains(g.GameId))
                .ToListAsync(cancellationToken);

            foreach (var game in games)
            {
                var score = scores[game.GameId];
                if (game.PopularityScore == score)
                {
                    continue;
                }

                game.PopularityScore = score;
                game.PopularityScoreUpdatedUtc = calculatedUtc;
                updated++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }
}
