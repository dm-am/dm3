using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Blog.Features.Popularity;

/// <summary>
/// Recalculates how popular each blog is.
/// </summary>
/// <remarks>
/// A blog's popularity is the number of people active on the site who read it;
/// the HTTP host and the seeder both call this one definition.
/// </remarks>
public interface IBlogPopularityProcessor
{
    /// <remarks>
    /// <paramref name="now" /> is passed in rather than read here because the two
    /// callers disagree about it on purpose: the host means the wall clock, the
    /// seeder means the instant its fixture is dated from.
    /// </remarks>
    /// <returns>How many blogs changed, and how many were considered.</returns>
    Task<(int Updated, int Total)> UpdateScoresAsync(
        DateTimeOffset now, CancellationToken ct = default);
}
