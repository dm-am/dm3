using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Popularity;

/// <remarks>
/// Counting and writing only: the rule lives in <see cref="IGamePopularityProcessor" />.
/// </remarks>
public interface IGamePopularityRepository
{
    /// <summary>
    /// Everything published and not deleted
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetScorableGameIds(CancellationToken cancellationToken = default);

    /// <summary>
    /// Per game, how many distinct people have a live character in it and have
    /// visited the site since <paramref name="activeSince" />.
    /// </summary>
    /// <remarks>
    /// NPCs are excluded: a master's cast does not measure how many people the game holds.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, int>> CountActivePlayers(
        IReadOnlyCollection<Guid> gameIds, DateTimeOffset activeSince, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per game, how many subscribers have visited the site since
    /// <paramref name="activeSince" />.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountActiveReaders(
        IReadOnlyCollection<Guid> gameIds, DateTimeOffset activeSince, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the scores, touching only the games whose value actually moved.
    /// </summary>
    /// <remarks>
    /// A game absent from <paramref name="scores"/> scores zero; <paramref name="calculatedUtc"/> is stamped on the games that changed.
    /// </remarks>
    /// <returns>Number of games whose score changed.</returns>
    Task<int> ApplyScores(
        IReadOnlyDictionary<Guid, int> scores,
        DateTimeOffset calculatedUtc,
        CancellationToken cancellationToken = default);
}
