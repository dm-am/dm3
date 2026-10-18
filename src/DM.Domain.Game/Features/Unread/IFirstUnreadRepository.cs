using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Unread;

/// <summary>
/// Repository for finding first unread posts and comments in games
/// </summary>
public interface IFirstUnreadRepository
{
    /// <summary>
    /// Get accessible room IDs in the order the game lists its rooms
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAccessibleRoomIds(Guid gameId, Guid userId);

    /// <summary>
    /// Find first unread post in rooms
    /// </summary>
    Task<FirstUnreadPostResult?> FindFirstUnreadPost(
        IReadOnlyList<Guid> roomIds,
        IDictionary<Guid, DateTime> lastReadTimes);

    /// <summary>
    /// Get first post in rooms (for anonymous users)
    /// </summary>
    Task<FirstUnreadPostResult?> GetFirstPostInRooms(IReadOnlyList<Guid> roomIds);

    /// <summary>
    /// Get last post in rooms (when no unread)
    /// </summary>
    Task<FirstUnreadPostResult?> GetLastPostInRooms(IReadOnlyList<Guid> roomIds);

    /// <summary>
    /// Where a reader who last read the room at <paramref name="lastRead"/> is put
    /// down in it: the first post written after that moment, or the last post of
    /// the room when there is none. Paging is the caller's.
    /// </summary>
    /// <param name="roomId">Room the reader may see</param>
    /// <param name="lastRead">Moment the room was last read, exclusive; MinValue for never</param>
    Task<FirstUnreadRoomPostResult> LocateInRoom(Guid roomId, DateTime lastRead);

    /// <summary>
    /// Moment (UTC) of the user's last post in the room that is still there; null
    /// when there is none.
    /// </summary>
    /// <param name="roomId">Room</param>
    /// <param name="userId">Author</param>
    Task<DateTime?> GetLastOwnPostUtc(Guid roomId, Guid userId);

    /// <summary>
    /// Find first unread comment
    /// </summary>
    Task<FirstUnreadCommentResult?> FindFirstUnreadComment(Guid gameId, DateTime lastRead);

    /// <summary>
    /// Get first comment (for anonymous users)
    /// </summary>
    Task<FirstUnreadCommentResult?> GetFirstComment(Guid gameId);

    /// <summary>
    /// Get last comment (when no unread)
    /// </summary>
    Task<FirstUnreadCommentResult?> GetLastComment(Guid gameId);
}

/// <summary>
/// Result of finding the first unread post in a game
/// </summary>
public class FirstUnreadPostResult
{
    /// <summary>
    /// Room containing the first unread post
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Post number (1-based) for pagination
    /// </summary>
    public int PostNumber { get; set; }

    /// <summary>
    /// Post identifier for scroll targeting
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Total number of unread posts in the game
    /// </summary>
    public int TotalUnreadCount { get; set; }

    /// <summary>
    /// Whether there are unread posts
    /// </summary>
    public bool HasUnread { get; set; }
}

/// <summary>
/// Result of finding the first unread post in one room
/// </summary>
public class FirstUnreadRoomPostResult
{
    /// <summary>
    /// The first unread post, or the last post when nothing is unread; null for a
    /// room without posts
    /// </summary>
    public Guid? PostId { get; set; }

    /// <summary>
    /// Number of that post in the room (1-based, the order the room is written
    /// in); 0 for a room without posts
    /// </summary>
    public int PostNumber { get; set; }

    /// <summary>
    /// Page holding that post at <see cref="PageSize"/>; 1 for a room without posts
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size the page was counted at
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Number of unread posts in the room
    /// </summary>
    public int TotalUnreadCount { get; set; }

    /// <summary>
    /// Whether there are unread posts (or any posts for anonymous users)
    /// </summary>
    public bool HasUnread { get; set; }
}

/// <summary>
/// Result of finding the first unread comment in a game
/// </summary>
public class FirstUnreadCommentResult
{
    /// <summary>
    /// Comment number (1-based) for pagination
    /// </summary>
    public int CommentNumber { get; set; }

    /// <summary>
    /// Comment identifier for scroll targeting
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Total number of unread comments in the game
    /// </summary>
    public int TotalUnreadCount { get; set; }

    /// <summary>
    /// Whether there are unread comments
    /// </summary>
    public bool HasUnread { get; set; }
}
