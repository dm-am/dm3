using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Popularity;

/// <summary>
/// Storage side of the game popularity score.
/// </summary>
/// <remarks>
/// Counting and writing only. What "active" means and how the parts add up is
/// the product rule, and it lives in <see cref="IGamePopularityProcessor" />.
/// </remarks>
public interface IGamePopularityRepository
{
    /// <summary>
    /// Identifiers of the games a score is kept for: everything published and
    /// not deleted, drafts excluded.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetScorableGameIds(CancellationToken cancellationToken = default);

    /// <summary>
    /// Per game, how many distinct people have a live character in it and have
    /// visited the site since <paramref name="activeSince" />.
    /// </summary>
    /// <remarks>
    /// NPCs are excluded: a master's cast is not a measure of how many people
    /// the game holds.
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
    /// <param name="scores">Score per game; a game absent from the map scores zero.</param>
    /// <param name="calculatedUtc">Moment stamped on the games that changed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of games whose score changed.</returns>
    Task<int> ApplyScores(
        IReadOnlyDictionary<Guid, int> scores,
        DateTimeOffset calculatedUtc,
        CancellationToken cancellationToken = default);
}
