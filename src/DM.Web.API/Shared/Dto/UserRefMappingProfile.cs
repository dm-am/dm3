using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Blog.Features.Blogs;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Mapping profile for UserRef - lightweight user references
/// </summary>
internal class UserRefMappingProfile : Profile
{
    /// <inheritdoc />
    public UserRefMappingProfile()
    {
        // GeneralUser (Domain DTO) → UserRef (API DTO)
        CreateMap<GeneralUser, UserRef>()
            .ForMember(d => d.Id, s => s.MapFrom(u => u.UserId))
            .ForMember(d => d.Username, s => s.MapFrom(u => u.Username))
            .ForMember(d => d.LastActivityUtc, s => s.MapFrom(u => u.LastActivityUtc))
            .ForMember(d => d.Role, s => s.MapFrom(u => u.Role))
            .ForMember(d => d.IsNewbie, s => s.MapFrom(u => u.IsNewbie));

        // UserReference (Domain DTO) → UserRef (API DTO). The two carry the same
        // five fields by design, so only the id rename needs saying.
        CreateMap<UserReference, UserRef>()
            .ForMember(d => d.Id, s => s.MapFrom(u => u.UserId));

        // GameAssistantInfo (Domain DTO) → UserRef (API DTO)
        CreateMap<GameAssistantInfo, UserRef>()
            .ForMember(d => d.Id, s => s.MapFrom(a => a.UserId))
            .ForMember(d => d.Username, s => s.MapFrom(a => a.Username))
            .ForMember(d => d.LastActivityUtc, s => s.MapFrom(a => a.LastActivityUtc))
            .ForMember(d => d.Role, s => s.MapFrom(a => a.Role))
            .ForMember(d => d.IsNewbie, s => s.MapFrom(a => a.IsNewbie));

        // BlogAssistantInfo (Domain DTO) → UserRef (API DTO)
        CreateMap<BlogAssistantInfo, UserRef>()
            .ForMember(d => d.Id, s => s.MapFrom(a => a.UserId))
            .ForMember(d => d.Username, s => s.MapFrom(a => a.Username))
            .ForMember(d => d.LastActivityUtc, s => s.MapFrom(a => a.LastActivityUtc))
            .ForMember(d => d.Role, s => s.MapFrom(a => a.Role))
            .ForMember(d => d.IsNewbie, s => s.MapFrom(a => a.IsNewbie));
    }
}
