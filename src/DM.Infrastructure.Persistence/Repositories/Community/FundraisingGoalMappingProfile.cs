using AutoMapper;
using DM.Domain.Community.Features.Fundraising;
using DbFundraisingGoal = DM.Infrastructure.Persistence.Entities.Community.FundraisingGoal;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class FundraisingGoalMappingProfile : Profile
{
    /// <inheritdoc />
    public FundraisingGoalMappingProfile()
    {
        // Title, GoalAmount, CollectedAmount and ModifiedUtc are mapped by convention
        CreateMap<DbFundraisingGoal, FundraisingGoal>();
    }
}
