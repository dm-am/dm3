namespace DM.Domain.Forum.Authorization;

/// <summary>
/// List of forum actions that required authorization
/// </summary>
public enum ForumIntention
{
    /// <summary>
    /// Create a topic on the forum
    /// </summary>
    CreateTopic = 0,

    /// <summary>
    /// Close, attach, delete and move any topic of the forum. The author closes
    /// and reopens their own topic through <see cref="TopicIntention.Close"/>.
    /// </summary>
    AdministrateTopics = 1
}
