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
    Task<LiveStats> GetLiveStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get leaderboards for a period: a year of 0 is all time, a null month the whole year
    /// </summary>
    Task<Leaderboards> GetLeaderboardsAsync(int year, int? month, CancellationToken ct = default);
}
