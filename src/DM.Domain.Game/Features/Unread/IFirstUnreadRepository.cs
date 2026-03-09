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
    /// Get accessible room IDs ordered by OrderNumber
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
