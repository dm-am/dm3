using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Forum.Features.Digests;

/// <summary>
/// Creates the "Итоги …" topics: one per closed calendar month and one per
/// closed year, in the news board.
/// </summary>
/// <remarks>
/// The digest's content is the period's live leaderboards, which the client
/// renders from the statistics API through the marker this leaves behind — board
/// data is computed, never copied into content — so the topic itself carries no
/// text.
///
/// Catch-up depth is exactly one period of each kind: a host that was down over
/// a boundary heals on its next start without backfilling history. Digest topics
/// are dated to the moment their period closed, so a yearly digest created in
/// July sits where January 1 puts it rather than surfacing as a fresh topic.
/// </remarks>
public interface IPeriodDigestProcessor
{
    /// <summary>
    /// Generates whatever digest the last closed month and the last closed year
    /// are still missing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Titles of the digests created, empty when both were already there.</returns>
    Task<IReadOnlyCollection<string>> EnsureClosedPeriodsAsync(CancellationToken cancellationToken = default);
}
