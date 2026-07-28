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
            .ForMember(d => d.Description, s => s.MapFrom(t => t.Text))
            // Filled by TopicApiService.EnrichPeriodDigests (one batch marker
            // lookup per response), not by the per-entity mapping.
            .ForMember(d => d.PeriodDigest, s => s.Ignore());

        CreateMap<DomainLastComment, LastTopicComment>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc));

        CreateMap<CreateTopicRequest, DomainCreateTopic>()
            .ForMember(d => d.BoardTitle, opt => opt.Ignore());

        CreateMap<UpdateTopicRequest, DomainUpdateTopic>()
            .ForMember(d => d.Text, s => s.MapFrom(t => t.Description))
            .ForMember(d => d.TopicId, opt => opt.Ignore())
            // The domain resolves a board by alias, title or id, and compares the
            // incoming value with the current board title to decide whether this is
            // a move. Mapping it from the board id made that comparison always
            // differ, so every edit carrying a board looked like a move.
            .ForMember(d => d.BoardTitle, s => s.MapFrom(t => t.Board));

        CreateMap<TopicsQuery, DomainTopicsQuery>();
    }
}
