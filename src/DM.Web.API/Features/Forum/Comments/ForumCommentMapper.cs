using Riok.Mapperly.Abstractions;
using DomainFirstUnreadComment = DM.Domain.Forum.Features.Comments.FirstUnreadComment;

namespace DM.Web.API.Features.Forum.Comments;

/// <summary>
/// Compile-time mapper from service DTO to API DTO for forum comments
/// </summary>
[Mapper]
internal partial class ForumCommentMapper
{
    /// <summary>
    /// Domain first-unread position to its response DTO
    /// </summary>
    public partial FirstUnreadComment ToFirstUnreadComment(DomainFirstUnreadComment position);
}
