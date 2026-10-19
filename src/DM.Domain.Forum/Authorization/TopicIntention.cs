namespace DM.Domain.Forum.Authorization;

/// <summary>
/// List of topic actions that requires authorization
/// </summary>
public enum TopicIntention
{
    CreateComment = 0,

    /// <summary>
    /// Edit topic title or description
    /// </summary>
    Edit = 1,

    Like = 2,

    /// <summary>
    /// Close or reopen the topic
    /// </summary>
    Close = 3
}
