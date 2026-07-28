namespace DM.Web.API.Features.Forum.Topics;

/// <summary>
/// Partial update of a forum topic
/// </summary>
/// <remarks>
/// Every field is optional: an omitted one keeps its current value. The read
/// model cannot be reused here — its <c>Title</c> initializes to an empty
/// string, so a request that only pins or closes a topic arrived as "set the
/// title to empty" and was rejected with 400.
/// </remarks>
public class UpdateTopicRequest
{
    /// <summary>
    /// Title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Description (topic body)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Board to move the topic to, by alias, title or id
    /// </summary>
    public string? Board { get; set; }

    /// <summary>
    /// Pin the topic to the top of its board
    /// </summary>
    public bool? IsAttached { get; set; }

    /// <summary>
    /// Close the topic for new comments
    /// </summary>
    public bool? IsClosed { get; set; }
}
