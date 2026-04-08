using System.Linq;
using AutoMapper;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence.Entities.Shared;
using TopicEntity = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <summary>
/// Profile for topic DTO and DAL mapping
/// </summary>
internal class TopicMappingProfile : Profile
{
    /// <inheritdoc />
    public TopicMappingProfile()
    {
        CreateMap<Comment, LastComment>()
            .ForMember(d => d.Id, opt => opt.Ignore());

        CreateMap<TopicEntity, Topic>()
            .ForMember(d => d.Id, s => s.MapFrom(t => t.TopicId))
            .ForMember(d => d.TopicNumber, s => s.MapFrom(t => t.TopicNumber))
            .ForMember(d => d.LastActivityUtc, s => s.MapFrom(t => t.LastComment == null
                ? t.CreatedUtc
                : t.LastComment.CreatedUtc))
            .ForMember(d => d.TotalCommentsCount, s => s.MapFrom(t => t.Comments.Count()))
            .ForMember(d => d.UnreadCommentsCount, opt => opt.Ignore())
            .ForMember(d => d.Likes, s => s.Ignore()); // Likes fetched via EntityType+EntityId pattern
    }
}
