using AutoMapper;
using DM.Web.API.Features.Forum.Boards;
using DomainTopic = DM.Domain.Forum.Features.Topics.Topic;
using DomainLastComment = DM.Domain.Forum.Features.Topics.LastComment;
using DomainCreateTopic = DM.Domain.Forum.Features.Topics.CreateTopic;
using DomainUpdateTopic = DM.Domain.Forum.Features.Topics.UpdateTopic;
using DomainTopicsQuery = DM.Domain.Forum.Features.Topics.TopicsQuery;

namespace DM.Web.API.Features.Forum.Topics;

/// <summary>
/// Mapping profile from Service DTO to API DTO for topics
/// </summary>
internal class TopicMappingProfile : Profile
{
    /// <inheritdoc />
    public TopicMappingProfile()
    {
        CreateMap<DomainTopic, Topic>()
            .ForMember(d => d.CommentsCount, s => s.MapFrom(t => t.TotalCommentsCount))
            .ForMember(d => d.Description, s => s.MapFrom(t => t.Text));

        CreateMap<DomainLastComment, LastTopicComment>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc));

        CreateMap<CreateTopicRequest, DomainCreateTopic>()
            .ForMember(d => d.BoardTitle, opt => opt.Ignore());

        CreateMap<Topic, DomainUpdateTopic>()
            .ForMember(d => d.Text, s => s.MapFrom(t => t.Description))
            .ForMember(d => d.TopicId, s => s.MapFrom(t => t.Id))
            .ForMember(d => d.BoardTitle, s => s.MapFrom(t => t.Board.Id));

        CreateMap<TopicsQuery, DomainTopicsQuery>();
    }
}
