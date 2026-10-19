using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Unread;

public interface IFirstUnreadRepository
{
    /// <summary>
    /// In the order the game lists its rooms
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAccessibleRoomIds(Guid gameId, Guid userId);

    Task<FirstUnreadPostResult?> FindFirstUnreadPost(
        IReadOnlyList<Guid> roomIds,
        IDictionary<Guid, DateTime> lastReadTimes);

    /// <summary>
    /// For guests
    /// </summary>
    Task<FirstUnreadPostResult?> GetFirstPostInRooms(IReadOnlyList<Guid> roomIds);

    /// <summary>
    /// When nothing is unread
    /// </summary>
    Task<FirstUnreadPostResult?> GetLastPostInRooms(IReadOnlyList<Guid> roomIds);

    /// <summary>
    /// The first post after <paramref name="lastRead"/>, or the last post; paging is the caller's
    /// </summary>
    /// <remarks>
    /// <paramref name="lastRead"/> is exclusive, MinValue for never.
    /// </remarks>
    Task<FirstUnreadRoomPostResult> LocateInRoom(Guid roomId, DateTime lastRead);

    /// <summary>
    /// The user's last post in the room that is still there
    /// </summary>
    Task<DateTime?> GetLastOwnPostUtc(Guid roomId, Guid userId);

    Task<FirstUnreadCommentResult?> FindFirstUnreadComment(Guid gameId, DateTime lastRead);

    /// <summary>
    /// For guests
    /// </summary>
    Task<FirstUnreadCommentResult?> GetFirstComment(Guid gameId);

    /// <summary>
    /// When nothing is unread
    /// </summary>
    Task<FirstUnreadCommentResult?> GetLastComment(Guid gameId);
}

public class FirstUnreadPostResult
{
    public Guid RoomId { get; set; }

    /// <summary>
    /// 1-based
    /// </summary>
    public int PostNumber { get; set; }

    public Guid PostId { get; set; }

    public int TotalUnreadCount { get; set; }

    public bool HasUnread { get; set; }
}

public class FirstUnreadRoomPostResult
{
    /// <summary>
    /// The first unread post, or the last post when nothing is unread; null for a
    /// room without posts
    /// </summary>
    public Guid? PostId { get; set; }

    /// <summary>
    /// 1-based; 0 for a room without posts
    /// </summary>
    public int PostNumber { get; set; }

    /// <summary>
    /// Page holding that post at <see cref="PageSize"/>; 1 for a room without posts
    /// </summary>
    public int Page { get; set; } = 1;

    public int PageSize { get; set; }

    public int TotalUnreadCount { get; set; }

    /// <summary>
    /// For a guest, whether there are any posts
    /// </summary>
    public bool HasUnread { get; set; }
}

public class FirstUnreadCommentResult
{
    /// <summary>
    /// 1-based
    /// </summary>
    public int CommentNumber { get; set; }

    public Guid CommentId { get; set; }

    public int TotalUnreadCount { get; set; }

    public bool HasUnread { get; set; }
}
