namespace DM.Domain.Community.Authorization;

/// <summary>
/// List of user endorsement actions that require authorization
/// </summary>
public enum UserEndorsementIntention
{
    /// <summary>
    /// Create user endorsement (requires having played together)
    /// </summary>
    Create = 1,

    /// <summary>
    /// Edit user endorsement (author only, within 24h)
    /// </summary>
    Edit = 2,

    /// <summary>
    /// Delete user endorsement (author or moderator)
    /// </summary>
    Delete = 3
}
