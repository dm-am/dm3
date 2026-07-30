using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Web.API.Shared.Dto;
using IAwardService = DM.Domain.Community.Features.Awards.IAwardService;

namespace DM.Web.API.Features.Community.Awards;

/// <inheritdoc />
internal class AwardApiService : IAwardApiService
{
    private readonly IAwardService _awardService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AwardApiService(IAwardService awardService, IMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<AwardType>> GetTypes()
    {
        var types = await _awardService.GetTypesAsync();
        var items = types.Select(_mapper.Map<AwardType>);
        return new ListEnvelope<AwardType>(items, null);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ContestSeries>> GetSeries()
    {
        var series = await _awardService.GetSeriesAsync();
        var items = series.Select(_mapper.Map<ContestSeries>);
        return new ListEnvelope<ContestSeries>(items, null);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<UserAward>> GetUserAwards(string username)
    {
        var list = await _awardService.GetUserAwardsAsync(username);
        var items = list.Select(_mapper.Map<UserAward>);
        return new ListEnvelope<UserAward>(items, null);
    }
}
