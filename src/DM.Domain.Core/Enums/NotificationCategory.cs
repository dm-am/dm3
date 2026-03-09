namespace DM.Domain.Core.Enums;

/// <summary>
/// Categories for bot notification delivery preferences
/// </summary>
public enum NotificationCategory
{
    /// <summary>
    /// Personal messages (NewMessage, LikedMessage)
    /// </summary>
    Messages = 1,

    /// <summary>
    /// Forum activity (comments, topics, likes)
    /// </summary>
    Forum = 2,

    /// <summary>
    /// Game activity (status changes, characters, invitations, post reviews)
    /// </summary>
    Games = 3,

    /// <summary>
    /// Subscription notifications (new topics/comments/games/posts in subscriptions)
    /// </summary>
    Subscriptions = 4,

    /// <summary>
    /// Security events (password changed, email changed, suspicious activity)
    /// </summary>
    Security = 5,

    /// <summary>
    /// Moderation events (tickets, warnings, bans) — moderators only
    /// </summary>
    Moderation = 6,

    /// <summary>
    /// Blog activity (publications, comments, likes, invitations)
    /// </summary>
    Blog = 7
}
