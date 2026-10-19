using System.Threading.Tasks;
using DM.Domain.Community.Features.Fundraising;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Fundraising;

public class FundraisingApiService
{
    private readonly IFundraisingGoalService _fundraisingGoalService;
    private readonly FundraisingMapper _mapper;

    public FundraisingApiService(
        IFundraisingGoalService fundraisingGoalService,
        FundraisingMapper mapper)
    {
        _fundraisingGoalService = fundraisingGoalService;
        _mapper = mapper;
    }

    public async Task<Envelope<Fundraising>> Get()
    {
        var goal = await _fundraisingGoalService.GetAsync();
        return new Envelope<Fundraising>(_mapper.ToFundraising(goal));
    }

    public async Task<Envelope<Fundraising>> Update(UpdateFundraisingRequest request)
    {
        var goal = await _fundraisingGoalService.UpdateAsync(_mapper.ToUpdateGoal(request));
        return new Envelope<Fundraising>(_mapper.ToFundraising(goal));
    }
}
