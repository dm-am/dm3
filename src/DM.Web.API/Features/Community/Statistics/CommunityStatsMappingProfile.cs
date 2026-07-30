using AutoMapper;
using DomainLeaderboardEntry = DM.Domain.Community.Features.Statistics.LeaderboardEntry;
using DomainLeaderboards = DM.Domain.Community.Features.Statistics.Leaderboards;
using DomainLiveStats = DM.Domain.Community.Features.Statistics.LiveStats;
using DomainPeriod = DM.Domain.Community.Features.Statistics.Period;
using DomainPostHighlight = DM.Domain.Community.Features.Statistics.PostHighlight;
using DomainStatValue = DM.Domain.Community.Features.Statistics.StatValue;
using DomainTotalsWithDelta = DM.Domain.Community.Features.Statistics.TotalsWithDelta;

namespace DM.Web.API.Features.Community.Statistics;

/// <summary>
/// AutoMapper profile for community statistics API DTOs
/// </summary>
internal class CommunityStatsMappingProfile : Profile
{
    /// <inheritdoc />
    public CommunityStatsMappingProfile()
    {
        // LastReviewedPost has never had a producer: the block shows the
        // week's best post, and the field stays in the contract as a null the
        // client already tolerates.
        CreateMap<DomainLiveStats, LiveStats>()
            .ForMember(d => d.LastReviewedPost, o => o.Ignore());
        CreateMap<DomainTotalsWithDelta, TotalsWithDelta>();
        CreateMap<DomainStatValue, StatValue>();
        CreateMap<DomainPostHighlight, PostHighlight>();

        CreateMap<DomainLeaderboards, Leaderboards>();
        CreateMap<DomainLeaderboardEntry, LeaderboardEntry>();
        CreateMap<DomainPeriod, Period>();
    }
}
