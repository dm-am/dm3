namespace DM.Services.Gaming.Authorization;

/// <summary>
/// Vote actions that require authorization
/// </summary>
public enum VoteIntention
{
    /// <summary>
    /// Create new vote on a post
    /// </summary>
    Create = 1,

    /// <summary>
    /// Delete existing vote
    /// </summary>
    Delete = 2
}
