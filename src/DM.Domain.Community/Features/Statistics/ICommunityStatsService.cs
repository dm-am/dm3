using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Statistics;

/// <summary>
/// Service for community statistics
/// </summary>
public interface ICommunityStatsService
{
    /// <summary>
    /// Get live community statistics
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task<LiveStats> GetLiveStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get leaderboards for a period
    /// </summary>
    /// <param name="year">Year, 0 for all time</param>
    /// <param name="month">Month, null for a whole year</param>
    /// <param name="ct">Cancellation token</param>
    Task<Leaderboards> GetLeaderboardsAsync(int year, int? month, CancellationToken ct = default);
}
