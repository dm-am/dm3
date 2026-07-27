using AutoMapper;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Community.Endorsements;

/// <inheritdoc />
internal class UserEndorsementMappingProfile : Profile
{
    /// <inheritdoc />
    public UserEndorsementMappingProfile()
    {
        // Author/TargetUser = GeneralUser → User via the existing
        // UserMappingProfile (single source of truth for avatar URLs
        // via AvatarPictureConverter).
        CreateMap<DM.Domain.Community.Features.UserEndorsements.UserEndorsement, UserEndorsement>()
            .ForMember(d => d.Author, opt => opt.MapFrom(e => e.Author))
            .ForMember(d => d.TargetUser, opt => opt.MapFrom(e => e.TargetUser));
    }
}
