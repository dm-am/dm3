namespace DM.Domain.Core.Enums;

/// <summary>
/// User's role in a blog (for API/DTO purposes)
/// </summary>
/// <remarks>
/// Privilege order: None &lt; Reader &lt; Mentor &lt; Assistant &lt; Owner
/// These roles are computed from multiple sources:
/// - None: No relation to the blog
/// - Reader: Subscriptions table (TargetType=Blog)
/// - Mentor: Blog.MentorId
/// - Assistant: BlogAssistants table
/// - Owner: Blog.AuthorId
/// </remarks>
public enum BlogRole
{
    /// <summary>
    /// No relation to the blog
    /// </summary>
    None = 0,

    /// <summary>
    /// Blog reader (subscribed via Subscriptions table)
    /// </summary>
    Reader = 1,

    /// <summary>
    /// Blog mentor (moderates newbie blogs, stored in Blog.MentorId)
    /// </summary>
    Mentor = 2,

    /// <summary>
    /// Blog assistant (can edit publications, stored in BlogAssistants table)
    /// </summary>
    Assistant = 3,

    /// <summary>
    /// Blog owner (creator, stored in Blog.AuthorId)
    /// </summary>
    Owner = 4
}
