using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Statistics;

/// <summary>
/// Service for community statistics
/// </summary>
public interface ICommunityStatsApiService
{
    /// <summary>
    /// Get live community statistics
    /// </summary>
    /// <returns>Live statistics</returns>
    Task<Envelope<LiveStats>> GetLiveStats();

    /// <summary>
    /// Get leaderboards for a specific period
    /// </summary>
    /// <param name="year">Year</param>
    /// <param name="month">Month (null for yearly)</param>
    /// <returns>Leaderboards data</returns>
    Task<Envelope<Leaderboards>> GetLeaderboards(int year, int? month);
}
