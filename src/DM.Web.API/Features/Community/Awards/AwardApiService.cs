using System.Linq;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using IAwardService = DM.Domain.Community.Features.Awards.IAwardService;

namespace DM.Web.API.Features.Community.Awards;

public class AwardApiService
{
    private readonly IAwardService _awardService;
    private readonly AwardMapper _mapper;

    public AwardApiService(IAwardService awardService, AwardMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<AwardType>> GetTypes()
    {
        var types = await _awardService.GetTypesAsync();
        var items = types.Select(_mapper.ToAwardType);
        return new ListEnvelope<AwardType>(items);
    }

    public async Task<ListEnvelope<ContestSeries>> GetSeries()
    {
        var series = await _awardService.GetSeriesAsync();
        var items = series.Select(_mapper.ToContestSeries);
        return new ListEnvelope<ContestSeries>(items);
    }

    public async Task<ListEnvelope<UserAward>> GetUserAwards(string username)
    {
        var list = await _awardService.GetUserAwardsAsync(username);
        var items = list.Select(_mapper.ToUserAward);
        return new ListEnvelope<UserAward>(items);
    }
}
