namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Type of entity that can be subscribed to
/// </summary>
public enum SubscriptionTargetType
{
    /// <summary>
    /// Game subscription (posts, status changes)
    /// </summary>
    Game = 1,

    /// <summary>
    /// Blog subscription (new publications)
    /// </summary>
    Blog = 2,

    /// <summary>
    /// Author subscription (new content from user)
    /// </summary>
    Author = 3,

    /// <summary>
    /// Forum board subscription (new topics)
    /// </summary>
    Board = 4,

    /// <summary>
    /// Forum topic subscription (new comments)
    /// </summary>
    Topic = 5,

    /// <summary>
    /// Blog publication subscription (new comments)
    /// </summary>
    Publication = 6,

    /// <summary>
    /// Game discussion subscription (comments in game discussion)
    /// </summary>
    GameDiscussion = 7,

    /// <summary>
    /// Blog discussion subscription (comments in blog discussion)
    /// </summary>
    BlogDiscussion = 8
}
