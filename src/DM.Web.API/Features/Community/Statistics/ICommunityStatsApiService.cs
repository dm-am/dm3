using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Statistics;

/// <summary>
/// Service for community statistics
/// </summary>
public interface ICommunityStatsApiService
{
    /// <summary>
    /// Get community statistics
    /// </summary>
    /// <returns>Community statistics</returns>
    Task<Envelope<CommunityStats>> GetStats();

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

    /// <summary>
    /// Get period report
    /// </summary>
    /// <param name="year">Year</param>
    /// <param name="month">Month (null for yearly)</param>
    /// <returns>Period report</returns>
    Task<Envelope<PeriodReport>> GetPeriodReport(int year, int? month);

    /// <summary>
    /// Compare two periods
    /// </summary>
    /// <param name="year1">First period year</param>
    /// <param name="month1">First period month (null for yearly)</param>
    /// <param name="year2">Second period year</param>
    /// <param name="month2">Second period month (null for yearly)</param>
    /// <returns>Period comparison</returns>
    Task<Envelope<PeriodComparison>> ComparePeriods(int year1, int? month1, int year2, int? month2);
}
