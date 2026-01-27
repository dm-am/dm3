namespace DM.Services.Community.BusinessProcesses.Polls;

/// <summary>
/// List of poll actions that require authorization
/// </summary>
public enum PollIntention
{
    /// <summary>
    /// Create new polls
    /// </summary>
    Create = 0,

    /// <summary>
    /// Take a vote in an active poll
    /// </summary>
    Vote = 1,

    /// <summary>
    /// Remove vote from a poll
    /// </summary>
    Unvote = 2,

    /// <summary>
    /// Edit existing poll
    /// </summary>
    Edit = 3,

    /// <summary>
    /// Delete existing poll
    /// </summary>
    Delete = 4
}