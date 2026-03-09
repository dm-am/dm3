namespace DM.Domain.Core.Enums;

/// <summary>
/// Type of content for polymorphic references (e.g., in Warning entity)
/// </summary>
public enum ContentType
{
    /// <summary>
    /// Game module
    /// </summary>
    Game = 1,

    /// <summary>
    /// Blog module
    /// </summary>
    Blog = 2,

    /// <summary>
    /// Game post
    /// </summary>
    Post = 3,

    /// <summary>
    /// Blog publication
    /// </summary>
    Publication = 4,

    /// <summary>
    /// Comment (forum topic, blog, game, publication)
    /// </summary>
    Comment = 5,

    /// <summary>
    /// Forum topic
    /// </summary>
    Topic = 6,

    /// <summary>
    /// Private or chat message
    /// </summary>
    Message = 7,

    /// <summary>
    /// Post review
    /// </summary>
    Review = 8,

    /// <summary>
    /// User profile
    /// </summary>
    Profile = 9
}
