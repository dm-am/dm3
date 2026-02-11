using AutoMapper;
using DM.Services.Game.Dto.Input;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// Mapping profile for game post models
/// </summary>
internal class PostProfile : Profile
{
    /// <inheritdoc />
    public PostProfile()
    {
        CreateMap<DM.Services.Game.Dto.Output.Post, Post>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(p => p.CreatedUtc))
            .ForMember(d => d.UpdatedUtc, s => s.MapFrom(p => p.ModifiedUtc))
            .ForMember(d => d.Commentary, s => s.MapFrom(p => p.Comment));

        CreateMap<CreatePostRequest, CreatePost>();
        CreateMap<Post, UpdatePost>();
    }
}
