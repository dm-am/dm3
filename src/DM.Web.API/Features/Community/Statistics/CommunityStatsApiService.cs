using System.Threading.Tasks;
using DM.Domain.Community.Features.Statistics;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Statistics;

public class CommunityStatsApiService
{
    private readonly ICommunityStatsService _statsService;
    private readonly CommunityStatsMapper _mapper;

    public CommunityStatsApiService(
        ICommunityStatsService statsService,
        CommunityStatsMapper mapper)
    {
        _statsService = statsService;
        _mapper = mapper;
    }

    public async Task<Envelope<LiveStats>> GetLiveStats()
    {
        var liveStats = await _statsService.GetLiveStatsAsync();
        return new Envelope<LiveStats>(_mapper.ToLiveStats(liveStats));
    }

    /// <summary>Without a month the leaderboards cover the whole year.</summary>
    public async Task<Envelope<Leaderboards>> GetLeaderboards(int year, int? month)
    {
        var leaderboards = await _statsService.GetLeaderboardsAsync(year, month);
        return new Envelope<Leaderboards>(_mapper.ToLeaderboards(leaderboards));
    }
}
