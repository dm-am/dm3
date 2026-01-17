using AutoMapper;
using DM.Services.Forum.Dto.Input;
using DM.Services.Forum.Dto.Output;

namespace DM.Web.API.Dto.Boards;

/// <summary>
/// Mapping profile from Service DTO to API DTO for topics
/// </summary>
internal class TopicProfile : Profile
{
    /// <inheritdoc />
    public TopicProfile()
    {
        CreateMap<DM.Services.Forum.Dto.Output.Topic, Topic>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(t => t.CreateDate))
            .ForMember(d => d.EditedUtc, s => s.MapFrom(t => t.LastUpdateDate))
            .ForMember(d => d.CommentsCount, s => s.MapFrom(t => t.TotalCommentsCount))
            .ForMember(d => d.Description, s => s.MapFrom(t => t.Text))
            .ForMember(d => d.Board, s => s.MapFrom(t => t.Forum));

        CreateMap<LastComment, LastTopicComment>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreateDate));

        CreateMap<Topic, CreateTopic>()
            .ForMember(d => d.Text, s => s.MapFrom(t => t.Description));

        CreateMap<Topic, UpdateTopic>()
            .ForMember(d => d.Text, s => s.MapFrom(t => t.Description))
            .ForMember(d => d.TopicId, s => s.MapFrom(t => t.Id))
            .ForMember(d => d.ForumTitle, s => s.MapFrom(t => t.Board.Id));
    }
}