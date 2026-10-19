using Riok.Mapperly.Abstractions;
using DomainLeaderboards = DM.Domain.Community.Features.Statistics.Leaderboards;
using DomainLiveStats = DM.Domain.Community.Features.Statistics.LiveStats;

namespace DM.Web.API.Features.Community.Statistics;

/// <summary>
/// Compile-time mapper for community statistics
/// </summary>
[Mapper]
public partial class CommunityStatsMapper
{
    /// <summary>
    /// Domain live stats to the API DTO
    /// </summary>
    public partial LiveStats ToLiveStats(DomainLiveStats liveStats);

    /// <summary>
    /// Domain leaderboards to the API DTO
    /// </summary>
    public partial Leaderboards ToLeaderboards(DomainLeaderboards leaderboards);
}
