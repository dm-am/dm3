using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Popularity;

/// <summary>
/// Popularity is the number of people active on the site who play in a game or read it.
/// </summary>
public interface IGamePopularityProcessor
{
    /// <remarks>
    /// <paramref name="now"/> is passed in: the host means the wall clock, the seeder the instant its fixture is dated from.
    /// </remarks>
    /// <returns>How many games changed, and how many were considered.</returns>
    Task<(int Updated, int Total)> UpdateScoresAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default);
}
