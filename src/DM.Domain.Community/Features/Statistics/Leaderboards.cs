using System;

namespace DM.Domain.Community.Features.Statistics;

/// <summary>
/// Leaderboards for a specific period
/// </summary>
public class Leaderboards
{
    /// <summary>
    /// Period information
    /// </summary>
    public Period Period { get; set; } = new();

    /// <summary>
    /// Top players by rating received
    /// </summary>
    public LeaderboardEntry[] TopPlayersByRating { get; set; } = Array.Empty<LeaderboardEntry>();

    /// <summary>
    /// Top games by total rating
    /// </summary>
    public LeaderboardEntry[] TopGamesByRating { get; set; } = Array.Empty<LeaderboardEntry>();

    /// <summary>
    /// Top players by posts count
    /// </summary>
    public LeaderboardEntry[] TopPlayersByPosts { get; set; } = Array.Empty<LeaderboardEntry>();

    /// <summary>
    /// Top games by posts count
    /// </summary>
    public LeaderboardEntry[] TopGamesByPosts { get; set; } = Array.Empty<LeaderboardEntry>();

    /// <summary>
    /// Top players by written text volume (sum of in-character post characters)
    /// </summary>
    public LeaderboardEntry[] TopPlayersByVolume { get; set; } = Array.Empty<LeaderboardEntry>();

    /// <summary>
    /// Top blogs by total rating (sum of likes received on the blog's publications)
    /// </summary>
    public LeaderboardEntry[] TopBlogsByRating { get; set; } = Array.Empty<LeaderboardEntry>();

    /// <summary>
    /// Top blogs by publications count
    /// </summary>
    public LeaderboardEntry[] TopBlogsByPosts { get; set; } = Array.Empty<LeaderboardEntry>();

    /// <summary>
    /// Top blog authors by written text volume (sum of publication content characters)
    /// </summary>
    public LeaderboardEntry[] TopBlogAuthorsByVolume { get; set; } = Array.Empty<LeaderboardEntry>();
}

/// <summary>
/// Single leaderboard entry
/// </summary>
public class LeaderboardEntry
{
    /// <summary>
    /// Ordinal rank (1-based, no gaps). Ties are ordered deterministically
    /// by name, each entry keeping its own number.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Entity identifier (user, game or blog)
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Short public identifier for game and blog entries (5-letter id used in
    /// canonical URLs). Null for player entries.
    /// </summary>
    public string? PublicId { get; set; }

    /// <summary>
    /// Display name (login or game title)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Score value (rating sum, posts count or character volume).
    /// Always positive: boards celebrate positive achievement only.
    /// </summary>
    public int Score { get; set; }
}

/// <summary>
/// Time period specification
/// </summary>
public class Period
{
    /// <summary>
    /// Year
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Month (null for yearly periods)
    /// </summary>
    public int? Month { get; set; }
}
