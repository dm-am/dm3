using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Statistics;

/// <summary>
/// Read model behind the community statistics block
/// </summary>
public interface ICommunityStatsRepository
{
    /// <summary>
    /// Count the live totals, today's deltas and the week's best post
    /// </summary>
    Task<LiveStats> GetLiveStats(CancellationToken ct = default);

    /// <summary>
    /// Aggregate every top list over the [start, end) window
    /// </summary>
    Task<Leaderboards> GetLeaderboards(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
}
