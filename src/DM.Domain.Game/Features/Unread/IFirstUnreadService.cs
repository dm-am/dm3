using System;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Unread;

public interface IFirstUnreadService
{
    Task<FirstUnreadPostResult> GetFirstUnreadPost(Guid gameId);

    /// <summary>
    /// Where the room opens when no page is named: the last post when all is read, the first for a guest.
    /// </summary>
    /// <remarks>
    /// A null <paramref name="pageSize"/> takes the reader's own setting.
    /// </remarks>
    Task<FirstUnreadRoomPostResult> GetFirstUnreadPostInRoom(Guid roomId, int? pageSize);

    Task<FirstUnreadCommentResult> GetFirstUnreadComment(Guid gameId);
}
