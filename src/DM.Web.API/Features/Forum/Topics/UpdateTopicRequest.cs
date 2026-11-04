namespace DM.Web.API.Features.Forum.Topics;

/// <summary>
/// Partial update of a forum topic
/// </summary>
/// <remarks>
/// Every field is optional: an omitted one keeps its current value.
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
