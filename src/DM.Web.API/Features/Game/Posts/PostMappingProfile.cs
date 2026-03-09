using AutoMapper;
using DM.Domain.Game.Features.Posts;
using DomainPost = DM.Domain.Game.Features.Games.Post;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// Mapping profile for game post models
/// </summary>
internal class PostMappingProfile : Profile
{
    /// <inheritdoc />
    public PostMappingProfile()
    {
        CreateMap<DomainPost, Post>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(p => p.CreatedUtc))
            .ForMember(d => d.UpdatedUtc, s => s.MapFrom(p => p.ModifiedUtc))
            .ForMember(d => d.Commentary, s => s.MapFrom(p => p.Comment));

        CreateMap<CreatePostRequest, CreatePost>();
        CreateMap<Post, UpdatePost>();
    }
}
