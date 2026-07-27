using AutoMapper;
using DM.Domain.Community.Features.Awards;
using ApiAwardType = DM.Web.API.Features.Community.Awards.AwardType;
using ApiContestSeries = DM.Web.API.Features.Community.Awards.ContestSeries;
using ApiUserAward = DM.Web.API.Features.Community.Awards.UserAward;
using DomainAwardType = DM.Domain.Community.Features.Awards.AwardType;
using DomainContestSeries = DM.Domain.Community.Features.Awards.ContestSeries;
using DomainUserAward = DM.Domain.Community.Features.Awards.UserAward;

namespace DM.Web.API.Features.Community.Awards;

/// <inheritdoc />
public class AwardMappingProfile : Profile
{
    /// <inheritdoc />
    public AwardMappingProfile()
    {
        CreateMap<DomainAwardType, ApiAwardType>();
        CreateMap<DomainContestSeries, ApiContestSeries>();
        // AwardedBy and Note are removed — not exposed in the public API (audit stays in the DB).
        CreateMap<DomainUserAward, ApiUserAward>();

        CreateMap<CreateAwardTypeRequest, CreateAwardType>();
        CreateMap<UpdateAwardTypeRequest, UpdateAwardType>()
            .ForMember(d => d.Id, opt => opt.Ignore()); // Id comes from the route, not the body

        CreateMap<CreateContestSeriesRequest, CreateContestSeries>();
        CreateMap<UpdateContestSeriesRequest, UpdateContestSeries>()
            .ForMember(d => d.Id, opt => opt.Ignore());
    }
}
