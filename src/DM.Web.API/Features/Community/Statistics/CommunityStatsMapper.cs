using Riok.Mapperly.Abstractions;
using DomainLeaderboards = DM.Domain.Community.Features.Statistics.Leaderboards;
using DomainLiveStats = DM.Domain.Community.Features.Statistics.LiveStats;

namespace DM.Web.API.Features.Community.Statistics;

/// <summary>
/// Compile-time mapper for community statistics
/// </summary>
[Mapper]
internal partial class CommunityStatsMapper
{
    /// <summary>
    /// Domain live stats to the API DTO. LastReviewedPost has never had a
    /// producer: the block shows the week's best post, and the field stays in
    /// the contract as a null the client already tolerates.
    /// </summary>
    [MapperIgnoreTarget(nameof(LiveStats.LastReviewedPost))]
    public partial LiveStats ToLiveStats(DomainLiveStats liveStats);

    /// <summary>
    /// Domain leaderboards to the API DTO
    /// </summary>
    public partial Leaderboards ToLeaderboards(DomainLeaderboards leaderboards);
}
