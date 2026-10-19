namespace DM.Domain.Community.Authorization;

/// <summary>
/// List of poll actions that require authorization
/// </summary>
public enum PollIntention
{
    Create = 0,

    /// <summary>
    /// Take a vote in an active poll
    /// </summary>
    Vote = 1,

    /// <summary>
    /// Remove vote from a poll
    /// </summary>
    Unvote = 2,

    Edit = 3,

    Delete = 4
}
