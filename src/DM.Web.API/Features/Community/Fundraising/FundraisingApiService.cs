using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.Fundraising;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Fundraising;

/// <inheritdoc />
internal class FundraisingApiService : IFundraisingApiService
{
    private readonly IFundraisingGoalService _fundraisingGoalService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public FundraisingApiService(
        IFundraisingGoalService fundraisingGoalService,
        IMapper mapper)
    {
        _fundraisingGoalService = fundraisingGoalService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<Fundraising>> Get()
    {
        var goal = await _fundraisingGoalService.GetAsync();
        return new Envelope<Fundraising>(_mapper.Map<Fundraising>(goal));
    }

    /// <inheritdoc />
    public async Task<Envelope<Fundraising>> Update(UpdateFundraisingRequest request)
    {
        var updateGoal = new UpdateFundraisingGoal
        {
            GoalAmount = request.GoalAmount,
            CollectedAmount = request.CollectedAmount
        };
        var goal = await _fundraisingGoalService.UpdateAsync(updateGoal);
        return new Envelope<Fundraising>(_mapper.Map<Fundraising>(goal));
    }
}
