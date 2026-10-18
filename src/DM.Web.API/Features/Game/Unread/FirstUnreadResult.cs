using System;

namespace DM.Web.API.Features.Game.Unread;

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
    /// Whether there are unread posts (or any posts for anonymous users)
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
    /// Page holding that post at <see cref="PageSize"/> (1-based); 1 for a room
    /// without posts
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Page size the page was counted at: the requested take, or the reader's own
    /// posts-per-page setting
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
    /// Whether there are unread comments (or any comments for anonymous users)
    /// </summary>
    public bool HasUnread { get; set; }
}
