using System;

namespace DM.Web.API.Dto.Games;

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
