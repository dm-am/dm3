using AutoMapper;
using DM.Domain.Core.Comments;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// Profile for comment mapping
/// </summary>
internal class CommentMappingProfile : Profile
{
    /// <inheritdoc />
    public CommentMappingProfile()
    {
        CreateMap<Entities.Shared.Comment, Comment>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CommentId))
            .ForMember(d => d.Likes, s => s.Ignore()); // Likes fetched via EntityType+EntityId pattern
    }
}
