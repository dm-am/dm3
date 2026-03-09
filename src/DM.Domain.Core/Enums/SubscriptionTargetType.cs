namespace DM.Domain.Core.Enums;

/// <summary>
/// Type of entity that can be subscribed to
/// </summary>
public enum SubscriptionTargetType
{
    /// <summary>
    /// Game subscription (sidebar + optional notifications for posts, status changes)
    /// </summary>
    Game = 1,

    /// <summary>
    /// Blog subscription (sidebar + optional notifications for new publications)
    /// </summary>
    Blog = 2,

    /// <summary>
    /// Forum topic subscription (notifications for new comments)
    /// </summary>
    Topic = 3,

    /// <summary>
    /// User subscription (notifications for new content from user)
    /// </summary>
    User = 4
}
