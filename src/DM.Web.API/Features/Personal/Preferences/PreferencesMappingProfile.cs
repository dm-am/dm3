using AutoMapper;
using ServiceUserSettings = DM.Domain.Core.Identity.UserSettings;
using ServicePagingSettings = DM.Domain.Core.Identity.PagingSettings;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// AutoMapper profile for Preferences mappings
/// </summary>
internal class PreferencesMappingProfile : Profile
{
    /// <inheritdoc />
    public PreferencesMappingProfile()
    {
        CreateMap<ServiceUserSettings, Preferences>()
            .ForMember(d => d.Theme, s => s.MapFrom(u => u.Theme))
            .ForMember(d => d.Paging, s => s.MapFrom(u => u.Paging ?? new ServicePagingSettings()));

        CreateMap<Preferences, ServiceUserSettings>()
            .ForMember(d => d.Id, s => s.Ignore())
            .ForMember(d => d.Theme, s => s.MapFrom(p => p.Theme))
            .ForMember(d => d.Paging, s => s.MapFrom(p => p.Paging));

        CreateMap<ServicePagingSettings, Paging>()
            .ForMember(d => d.PostsPerPage, s => s.MapFrom(p => p.PostsPerPage > 0 ? p.PostsPerPage : 10))
            .ForMember(d => d.CommentsPerPage, s => s.MapFrom(p => p.CommentsPerPage > 0 ? p.CommentsPerPage : 10))
            .ForMember(d => d.TopicsPerPage, s => s.MapFrom(p => p.TopicsPerPage > 0 ? p.TopicsPerPage : 10))
            .ForMember(d => d.MessagesPerPage, s => s.MapFrom(p => p.MessagesPerPage > 0 ? p.MessagesPerPage : 10))
            .ForMember(d => d.EntitiesPerPage, s => s.MapFrom(p => p.EntitiesPerPage > 0 ? p.EntitiesPerPage : 10));

        CreateMap<Paging, ServicePagingSettings>();
    }
}
