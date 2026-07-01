using AutoMapper;
using EntityAwardType = DM.Infrastructure.Persistence.Entities.Community.AwardType;
using EntityContestSeries = DM.Infrastructure.Persistence.Entities.Community.ContestSeries;
using EntityUserAward = DM.Infrastructure.Persistence.Entities.Community.UserAward;
using DomainAwardType = DM.Domain.Community.Features.Awards.AwardType;
using DomainContestSeries = DM.Domain.Community.Features.Awards.ContestSeries;
using DomainUserAward = DM.Domain.Community.Features.Awards.UserAward;

namespace DM.Infrastructure.Persistence.Repositories.Community;

internal class AwardMappingProfile : Profile
{
    public AwardMappingProfile()
    {
        CreateMap<EntityAwardType, DomainAwardType>()
            .ForMember(d => d.Id, s => s.MapFrom(t => t.AwardTypeId));

        CreateMap<EntityContestSeries, DomainContestSeries>()
            .ForMember(d => d.Id, s => s.MapFrom(s2 => s2.ContestSeriesId));

        // AwardedBy and Note не маппятся в Domain — поля остаются только
        // в БД для audit. Domain.UserAward не имеет этих свойств.
        CreateMap<EntityUserAward, DomainUserAward>()
            .ForMember(d => d.Id, s => s.MapFrom(a => a.UserAwardId))
            .ForMember(d => d.Type, s => s.MapFrom(a => a.AwardType))
            .ForMember(d => d.ContestSeries, s => s.MapFrom(a => a.ContestSeries))
            .ForMember(d => d.User, s => s.MapFrom(a => a.User));
    }
}
