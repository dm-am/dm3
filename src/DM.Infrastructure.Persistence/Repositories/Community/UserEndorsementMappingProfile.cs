using AutoMapper;
using DM.Domain.Community.Features.UserEndorsements;
using DbUserEndorsement = DM.Infrastructure.Persistence.Entities.Community.UserEndorsement;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <summary>
/// AutoMapper profile for user endorsement mappings
/// </summary>
internal class UserEndorsementMappingProfile : Profile
{
    public UserEndorsementMappingProfile()
    {
        CreateMap<DbUserEndorsement, UserEndorsement>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.UserEndorsementId))
            .ForMember(d => d.TargetUser, s => s.MapFrom(e => e.TargetUser));
    }
}
