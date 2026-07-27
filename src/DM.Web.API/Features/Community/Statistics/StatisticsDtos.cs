using System;

namespace DM.Web.API.Features.Community.Statistics;

#region Live Stats

/// <summary>
/// Live community statistics (updated frequently)
/// </summary>
public class LiveStats
{
    /// <summary>
    /// Number of users currently online
    /// </summary>
    public int Online { get; set; }

    /// <summary>
    /// Total counts with today's changes
    /// </summary>
    public TotalsWithDelta Totals { get; set; } = new();

    /// <summary>
    /// Most recently reviewed post
    /// </summary>
    public PostHighlight? LastReviewedPost { get; set; }

    /// <summary>
    /// Best post of the week by rating
    /// </summary>
    public PostHighlight? WeeklyBestPost { get; set; }
}

/// <summary>
/// Total counts with daily change delta
/// </summary>
public class TotalsWithDelta
{
    /// <summary>
    /// Total users count
    /// </summary>
    public StatValue Users { get; set; } = new();

    /// <summary>
    /// Total characters count
    /// </summary>
    public StatValue Characters { get; set; } = new();

    /// <summary>
    /// Total games count
    /// </summary>
    public StatValue Games { get; set; } = new();

    /// <summary>
    /// Total game posts count
    /// </summary>
    public StatValue GamePosts { get; set; } = new();

    /// <summary>
    /// Total blogs count
    /// </summary>
    public StatValue Blogs { get; set; } = new();

    /// <summary>
    /// Total blog publications count
    /// </summary>
    public StatValue Publications { get; set; } = new();
}

/// <summary>
/// Statistical value with today's delta
/// </summary>
public class StatValue
{
    /// <summary>
    /// Current total value
    /// </summary>
    public long Value { get; set; }

    /// <summary>
    /// Change from start of day (UTC)
    /// </summary>
    public int TodayDelta { get; set; }
}

/// <summary>
/// Highlighted post information
/// </summary>
public class PostHighlight
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = string.Empty;

    /// <summary>
    /// Post author username
    /// </summary>
    public string AuthorUsername { get; set; } = string.Empty;

    /// <summary>
    /// Total rating sum (for weekly best)
    /// </summary>
    public int? RatingSum { get; set; }
}

#endregion

#region Leaderboards

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
    /// Top players by written text volume (sum of in-character post characters).
    /// The "Самый многопишущий игрок" board from the statistics content block.
    /// </summary>
    public LeaderboardEntry[] TopPlayersByVolume { get; set; } = Array.Empty<LeaderboardEntry>();

    /// <summary>
    /// Top blogs by total rating (sum of likes received on the blog's
    /// publications). The blog analog of TopGamesByRating.
    /// </summary>
    public LeaderboardEntry[] TopBlogsByRating { get; set; } = Array.Empty<LeaderboardEntry>();

    /// <summary>
    /// Top blogs by publications count. The blog analog of TopGamesByPosts.
    /// </summary>
    public LeaderboardEntry[] TopBlogsByPosts { get; set; } = Array.Empty<LeaderboardEntry>();

    /// <summary>
    /// Top blog authors by written text volume (sum of publication Content
    /// characters). The blog analog of TopPlayersByVolume.
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
    /// Entity identifier (user or game)
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

#endregion
