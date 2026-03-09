namespace DM.Domain.Game.Authorization;

/// <summary>
/// List of game review actions that require authorization
/// </summary>
public enum GameReviewIntention
{
    /// <summary>
    /// Create game review (requires having at least one post in the game)
    /// </summary>
    Create = 0,

    /// <summary>
    /// Edit existing game review (author only, within time window)
    /// </summary>
    Edit = 1,

    /// <summary>
    /// Delete game review (author or moderator)
    /// </summary>
    Delete = 2
}
