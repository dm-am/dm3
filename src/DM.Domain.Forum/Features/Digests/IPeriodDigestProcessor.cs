using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Forum.Features.Digests;

/// <summary>
/// Creates the "Итоги …" topics: one per closed calendar month and one per
/// closed year, in the news board.
/// </summary>
/// <remarks>
/// The topic carries no text: the client renders the period's live leaderboards
/// through the marker this leaves behind. Catch-up depth is one period of each
/// kind, and a digest is dated to the moment its period closed.
/// </remarks>
public interface IPeriodDigestProcessor
{
    /// <summary>
    /// Generates whatever digest the last closed month and the last closed year
    /// are still missing.
    /// </summary>
    /// <returns>Titles of the digests created, empty when both were already there.</returns>
    Task<IReadOnlyCollection<string>> EnsureClosedPeriodsAsync(CancellationToken ct = default);
}
