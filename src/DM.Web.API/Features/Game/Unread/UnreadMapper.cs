using Riok.Mapperly.Abstractions;
using DomainFirstUnreadCommentResult = DM.Domain.Game.Features.Unread.FirstUnreadCommentResult;
using DomainFirstUnreadPostResult = DM.Domain.Game.Features.Unread.FirstUnreadPostResult;
using DomainFirstUnreadRoomPostResult = DM.Domain.Game.Features.Unread.FirstUnreadRoomPostResult;

namespace DM.Web.API.Features.Game.Unread;

/// <summary>
/// Compile-time mapper for first-unread lookups
/// </summary>
[Mapper]
internal partial class UnreadMapper
{
    /// <summary>
    /// Domain first-unread post lookup to its response DTO
    /// </summary>
    public partial FirstUnreadPostResult ToResponse(DomainFirstUnreadPostResult result);

    /// <summary>
    /// Domain first-unread post lookup in one room to its response DTO
    /// </summary>
    public partial FirstUnreadRoomPostResult ToResponse(DomainFirstUnreadRoomPostResult result);

    /// <summary>
    /// Domain first-unread comment lookup to its response DTO
    /// </summary>
    public partial FirstUnreadCommentResult ToResponse(DomainFirstUnreadCommentResult result);
}
