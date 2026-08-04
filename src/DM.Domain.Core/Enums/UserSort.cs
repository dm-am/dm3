namespace DM.Domain.Core.Enums;

/// <summary>
/// User list sorting options
/// </summary>
public enum UserSort
{
    /// <summary>
    /// Sort by username alphabetically
    /// </summary>
    Name = 0,

    /// <summary>
    /// Sort by rating (post review score sum) descending
    /// </summary>
    Rating = 1,

    /// <summary>
    /// Sort by last activity date descending (default)
    /// </summary>
    LastActivity = 2,

    /// <summary>
    /// Sort by registration date descending
    /// </summary>
    Registered = 3,

    /// <summary>
    /// Sort by number of games hosting (as master/assistant) descending
    /// </summary>
    GamesHosting = 4,

    /// <summary>
    /// Sort by popularity (active subscribers within 30 days)
    /// </summary>
    Popularity = 5,

    /// <summary>
    /// Sort by number of blogs hosting descending
    /// </summary>
    BlogsHosting = 6,

    /// <summary>
    /// Sort by number of games playing (as player/reader) descending
    /// </summary>
    GamesPlaying = 7
}
