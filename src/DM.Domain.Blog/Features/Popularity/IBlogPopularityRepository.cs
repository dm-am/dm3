using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Blog.Features.Popularity;

/// <summary>
/// Storage side of the blog popularity score.
/// </summary>
/// <remarks>
/// Counting and writing only. What "active" means is the product rule, and it
/// lives in <see cref="IBlogPopularityProcessor" />.
/// </remarks>
public interface IBlogPopularityRepository
{
    /// <summary>
    /// Identifiers of the blogs a score is kept for.
    /// </summary>
    /// <remarks>
    /// Closed blogs included: a finished blog still has the readers it earned,
    /// and dropping it out of the count would empty its place in the listing the
    /// moment its author stopped writing.
    /// </remarks>
    Task<IReadOnlyCollection<Guid>> GetScorableBlogIds(CancellationToken cancellationToken = default);

    /// <summary>
    /// Per blog, how many subscribers have visited the site since
    /// <paramref name="activeSince" />.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountActiveReaders(
        IReadOnlyCollection<Guid> blogIds, DateTimeOffset activeSince, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the scores, touching only the blogs whose value actually moved.
    /// </summary>
    /// <param name="scores">Score per blog; a blog absent from the map scores zero.</param>
    /// <param name="calculatedUtc">Moment stamped on the blogs that changed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of blogs whose score changed.</returns>
    Task<int> ApplyScores(
        IReadOnlyDictionary<Guid, int> scores,
        DateTimeOffset calculatedUtc,
        CancellationToken cancellationToken = default);
}
