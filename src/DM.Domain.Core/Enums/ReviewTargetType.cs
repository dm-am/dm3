namespace DM.Domain.Core.Enums;

/// <summary>
/// Type of entity that can receive reviews
/// </summary>
public enum ReviewTargetType
{
    /// <summary>
    /// Platform/site review (no specific target entity)
    /// </summary>
    Platform = 0,

    /// <summary>
    /// Review of a user profile
    /// </summary>
    User = 1,

    /// <summary>
    /// Review of a game
    /// </summary>
    Game = 2,

    /// <summary>
    /// Review of a game post (formerly Vote)
    /// </summary>
    Post = 3
}
