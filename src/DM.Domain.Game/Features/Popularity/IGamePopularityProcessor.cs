using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Popularity;

/// <summary>
/// Recalculates how popular each game is.
/// </summary>
/// <remarks>
/// The definition — a game's popularity is the number of people active on the
/// site who either play in it or read it — used to exist twice, written out
/// query for query in a background job of the HTTP host and in the seeder, with
/// the window they compared against already diverged. Both call this now.
/// </remarks>
public interface IGamePopularityProcessor
{
    /// <summary>
    /// Recalculates every game's score.
    /// </summary>
    /// <param name="now">
    /// Moment the pass runs at. Passed in rather than read here because the two
    /// callers disagree about it on purpose: the host means the wall clock, the
    /// seeder means the instant its fixture is dated from.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many games changed, and how many were considered.</returns>
    Task<(int Updated, int Total)> UpdateScoresAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default);
}
