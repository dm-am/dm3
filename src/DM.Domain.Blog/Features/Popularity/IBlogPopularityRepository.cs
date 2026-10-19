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
    Task<IReadOnlyCollection<Guid>> GetScorableBlogIds(CancellationToken ct = default);

    /// <summary>
    /// Per blog, how many subscribers have visited the site since
    /// <paramref name="activeSince" />.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountActiveReaders(
        IReadOnlyCollection<Guid> blogIds, DateTimeOffset activeSince, CancellationToken ct = default);

    /// <summary>
    /// Writes the scores, touching only the blogs whose value actually moved and
    /// stamping them with <paramref name="calculatedUtc" />. A blog absent from
    /// <paramref name="scores" /> scores zero.
    /// </summary>
    /// <returns>Number of blogs whose score changed.</returns>
    Task<int> ApplyScores(
        IReadOnlyDictionary<Guid, int> scores,
        DateTimeOffset calculatedUtc,
        CancellationToken ct = default);
}
