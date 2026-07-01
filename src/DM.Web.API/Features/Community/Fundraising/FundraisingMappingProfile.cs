using AutoMapper;
using DomainFundraisingGoal = DM.Domain.Community.Features.Fundraising.FundraisingGoal;

namespace DM.Web.API.Features.Community.Fundraising;

/// <inheritdoc />
internal class FundraisingMappingProfile : Profile
{
    /// <inheritdoc />
    public FundraisingMappingProfile()
    {
        CreateMap<DomainFundraisingGoal, Fundraising>();
    }
}
