using System.Linq;
using AutoMapper;
using TopicDal = DM.Services.DataAccess.BusinessObjects.Boards.Topic;
using TopicDto = DM.Services.Forum.Dto.Output.Topic;
using DM.Services.Forum.Dto.Output;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Forum.Dto;

/// <summary>
/// Profile for topic DTO and DAL mapping
/// </summary>
internal class TopicProfile : Profile
{
    /// <inheritdoc />
    public TopicProfile()
    {
        CreateMap<Comment, LastComment>();

        CreateMap<TopicDal, TopicDto>()
            .ForMember(d => d.Id, s => s.MapFrom(t => t.TopicId))
            .ForMember(d => d.LastActivityUtc, s => s.MapFrom(t => t.LastComment == null
                ? t.CreatedUtc
                : t.LastComment.CreatedUtc))
            .ForMember(d => d.TotalCommentsCount, s => s.MapFrom(t => t.Comments.Count()))
            .ForMember(d => d.Likes, s => s.Ignore()); // Likes fetched via EntityType+EntityId pattern
    }
}