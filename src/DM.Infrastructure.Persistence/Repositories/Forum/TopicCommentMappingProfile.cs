using AutoMapper;
using DM.Domain.Forum.Features.Comments;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <summary>
/// AutoMapper profile for topic comment entities
/// </summary>
internal class TopicCommentMappingProfile : Profile
{
    public TopicCommentMappingProfile()
    {
        CreateMap<DbComment, TopicCommentToDelete>()
            .IncludeBase<DbComment, DM.Domain.Core.Comments.Comment>()
            .ForMember(d => d.IsLastComment, s => s.Ignore()); // Set manually
    }
}
