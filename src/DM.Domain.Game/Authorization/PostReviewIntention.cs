namespace DM.Domain.Game.Authorization;

/// <summary>
/// List of post review actions that require authorization
/// </summary>
public enum PostReviewIntention
{
    /// <summary>
    /// Create post review
    /// </summary>
    Create = 0,

    /// <summary>
    /// Edit existing post review (author only, within time window)
    /// </summary>
    Edit = 1,

    /// <summary>
    /// Delete post review (author or moderator)
    /// </summary>
    Delete = 2
}
