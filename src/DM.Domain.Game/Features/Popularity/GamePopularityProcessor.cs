using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;

namespace DM.Domain.Game.Features.Popularity;

/// <inheritdoc />
internal class GamePopularityProcessor : IGamePopularityProcessor
{
    private readonly IGamePopularityRepository _repository;

    /// <inheritdoc cref="GamePopularityProcessor" />
    public GamePopularityProcessor(IGamePopularityRepository repository) => _repository = repository;

    /// <inheritdoc />
    public async Task<(int Updated, int Total)> UpdateScoresAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var gameIds = await _repository.GetScorableGameIds(cancellationToken);
        if (gameIds.Count == 0)
        {
            return (0, 0);
        }

        // "Active" is one product decision for the whole server, not this job's
        // own idea of it: the same threshold orders subscribers and filters
        // listings, and a second copy of it drifts the day the first one moves.
        var activeSince = now - ActivityPolicy.ActivePeriod;

        var players = await _repository.CountActivePlayers(gameIds, activeSince, cancellationToken);
        var readers = await _repository.CountActiveReaders(gameIds, activeSince, cancellationToken);

        var scores = new Dictionary<Guid, int>(gameIds.Count);
        foreach (var gameId in gameIds)
        {
            players.TryGetValue(gameId, out var playerCount);
            readers.TryGetValue(gameId, out var readerCount);
            scores[gameId] = playerCount + readerCount;
        }

        var updated = await _repository.ApplyScores(scores, now, cancellationToken);
        return (updated, gameIds.Count);
    }
}
