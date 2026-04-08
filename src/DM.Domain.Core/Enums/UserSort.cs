using System.ComponentModel;

namespace DM.Domain.Core.Enums;

/// <summary>
/// User list sorting options
/// </summary>
public enum UserSort
{
    /// <summary>
    /// Sort by username alphabetically
    /// </summary>
    [Description("По имени")]
    Name = 0,

    /// <summary>
    /// Sort by rating (post review score sum) descending
    /// </summary>
    [Description("По рейтингу")]
    Rating = 1,

    /// <summary>
    /// Sort by last activity date descending (default)
    /// </summary>
    [Description("По активности")]
    LastActivity = 2,

    /// <summary>
    /// Sort by registration date descending
    /// </summary>
    [Description("По регистрации")]
    Registered = 3,

    /// <summary>
    /// Sort by number of games hosting (as master/assistant) descending
    /// </summary>
    [Description("По играм (ведущий)")]
    GamesHosting = 4,

    /// <summary>
    /// Sort by popularity (active subscribers within 30 days)
    /// </summary>
    [Description("По популярности")]
    Popularity = 5,

    /// <summary>
    /// Sort by number of blogs hosting descending
    /// </summary>
    [Description("По блогам")]
    BlogsHosting = 6,

    /// <summary>
    /// Sort by number of games playing (as player/reader) descending
    /// </summary>
    [Description("По играм (игрок)")]
    GamesPlaying = 7
}
