using AutoMapper;
using DM.Services.Game.Dto.Output;
using DbPost = DM.Services.DataAccess.BusinessObjects.Games.Posts.Post;

namespace DM.Services.Game.Dto;

/// <summary>
/// Profile for post DTO and DAL mapping
/// </summary>
internal class PostProfile : Profile
{
    /// <inheritdoc />
    public PostProfile()
    {
        CreateMap<DbPost, Post>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PostId))
            .ForMember(d => d.Comment, s => s.MapFrom(p => p.Commentary));
    }
}
