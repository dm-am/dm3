using AutoMapper;
using DM.Services.Gaming.Dto.Input;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// Mapping profile for game post models
/// </summary>
internal class PostProfile : Profile
{
    /// <inheritdoc />
    public PostProfile()
    {
        CreateMap<DM.Services.Gaming.Dto.Output.Post, Post>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(p => p.CreateDate))
            .ForMember(d => d.UpdatedUtc, s => s.MapFrom(p => p.LastUpdateDate))
            .ForMember(d => d.Commentary, s => s.MapFrom(p => p.Comment));

        CreateMap<Post, CreatePost>();
        CreateMap<Post, UpdatePost>();
    }
}
