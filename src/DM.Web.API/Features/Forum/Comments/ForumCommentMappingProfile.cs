using AutoMapper;
using DomainFirstUnreadComment = DM.Domain.Forum.Features.Comments.FirstUnreadComment;

namespace DM.Web.API.Features.Forum.Comments;

/// <summary>
/// Mapping profile from Service DTO to API DTO for forum comments
/// </summary>
internal class ForumCommentMappingProfile : Profile
{
    /// <inheritdoc />
    public ForumCommentMappingProfile()
    {
        CreateMap<DomainFirstUnreadComment, FirstUnreadComment>();
    }
}
