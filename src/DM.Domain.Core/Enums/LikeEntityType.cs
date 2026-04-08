namespace DM.Domain.Core.Enums;

/// <summary>
/// Type of entity that can receive likes
/// </summary>
public enum LikeEntityType
{
    /// <summary>
    /// Unknown or unspecified entity type
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Comment (topic, blog, publication, or game)
    /// </summary>
    Comment = 1,

    /// <summary>
    /// Forum topic
    /// </summary>
    Topic = 2,

    /// <summary>
    /// Private message
    /// </summary>
    Message = 3,

    /// <summary>
    /// Blog publication
    /// </summary>
    Publication = 4,

    /// <summary>
    /// Game post
    /// </summary>
    Post = 5,

    /// <summary>
    /// post review (review of a game post with rating)
    /// </summary>
    PostReview = 6
}
