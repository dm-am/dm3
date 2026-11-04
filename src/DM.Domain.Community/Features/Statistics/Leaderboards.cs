using System;
using System.Collections.Generic;

namespace DM.Domain.Community.Features.Statistics;

/// <summary>
/// Leaderboards for a specific period
/// </summary>
/// <remarks>
/// The repository fills the boards as the database returns them: positive scores
/// only, cut to the board size. The service sorts them, assigns the display ranks
/// and names the period.
/// </remarks>
public class Leaderboards
{
    /// <summary>
    /// How many entries a board holds. One number for every board: they are
    /// read side by side, so a board cut to a different length reads as
    /// missing data rather than as a shorter list.
    /// </summary>
    public const int BoardSize = 10;

    /// <summary>
    /// Period information
    /// </summary>
    public Period Period { get; set; } = new();

    /// <summary>
    /// Top players by rating received
    /// </summary>
    public List<LeaderboardEntry> TopPlayersByRating { get; init; } = [];

    /// <summary>
    /// Top games by total rating
    /// </summary>
    public List<LeaderboardEntry> TopGamesByRating { get; init; } = [];

    /// <summary>
    /// Top players by posts count
    /// </summary>
    public List<LeaderboardEntry> TopPlayersByPosts { get; init; } = [];

    /// <summary>
    /// Top games by posts count
    /// </summary>
    public List<LeaderboardEntry> TopGamesByPosts { get; init; } = [];

    /// <summary>
    /// Top players by written text volume (sum of in-character post characters)
    /// </summary>
    public List<LeaderboardEntry> TopPlayersByVolume { get; init; } = [];

    /// <summary>
    /// Top blogs by total rating (sum of likes received on the blog's publications)
    /// </summary>
    public List<LeaderboardEntry> TopBlogsByRating { get; init; } = [];

    /// <summary>
    /// Top blogs by publications count
    /// </summary>
    public List<LeaderboardEntry> TopBlogsByPosts { get; init; } = [];

    /// <summary>
    /// Top blog authors by written text volume (sum of publication content characters)
    /// </summary>
    public List<LeaderboardEntry> TopBlogAuthorsByVolume { get; init; } = [];
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
