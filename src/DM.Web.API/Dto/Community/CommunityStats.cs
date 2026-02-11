using System;

namespace DM.Web.API.Dto.Community;

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
    /// Post author login
    /// </summary>
    public string AuthorLogin { get; set; } = string.Empty;

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
}

/// <summary>
/// Single leaderboard entry
/// </summary>
public class LeaderboardEntry
{
    /// <summary>
    /// Position in the leaderboard (1-based)
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Entity identifier (user or game)
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Display name (login or game title)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Picture URL (avatar or game cover)
    /// </summary>
    public string? PictureUrl { get; set; }

    /// <summary>
    /// Score value (rating sum or posts count)
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

#region Reports

/// <summary>
/// Period report with aggregated statistics
/// </summary>
public class PeriodReport
{
    /// <summary>
    /// Report period
    /// </summary>
    public Period Period { get; set; } = new();

    /// <summary>
    /// Number of new user registrations
    /// </summary>
    public int Registrations { get; set; }

    /// <summary>
    /// Number of new games created
    /// </summary>
    public int GamesCreated { get; set; }

    /// <summary>
    /// Number of new game posts
    /// </summary>
    public int GamePosts { get; set; }

    /// <summary>
    /// Number of reviews given
    /// </summary>
    public int Reviews { get; set; }

    /// <summary>
    /// Average daily active users
    /// </summary>
    public int AverageDailyUsers { get; set; }
}

/// <summary>
/// Period comparison results
/// </summary>
public class PeriodComparison
{
    /// <summary>
    /// First period
    /// </summary>
    public PeriodReport Period1 { get; set; } = new();

    /// <summary>
    /// Second period
    /// </summary>
    public PeriodReport Period2 { get; set; } = new();

    /// <summary>
    /// Growth percentages
    /// </summary>
    public GrowthMetrics Growth { get; set; } = new();
}

/// <summary>
/// Growth metrics between periods
/// </summary>
public class GrowthMetrics
{
    /// <summary>
    /// Registration growth percentage
    /// </summary>
    public double RegistrationsGrowth { get; set; }

    /// <summary>
    /// Games creation growth percentage
    /// </summary>
    public double GamesGrowth { get; set; }

    /// <summary>
    /// Posts growth percentage
    /// </summary>
    public double PostsGrowth { get; set; }

    /// <summary>
    /// Reviews growth percentage
    /// </summary>
    public double ReviewsGrowth { get; set; }
}

#endregion

#region Legacy (Backward Compatibility)

/// <summary>
/// Legacy community statistics response (prefer LiveStats, Leaderboards, PeriodReport)
/// </summary>
public class CommunityStats
{
    /// <summary>
    /// Current statistics
    /// </summary>
    public CurrentStats Current { get; set; } = new();

    /// <summary>
    /// Monthly statistics
    /// </summary>
    public PeriodStats Monthly { get; set; } = new();

    /// <summary>
    /// Yearly statistics
    /// </summary>
    public PeriodStats Yearly { get; set; } = new();

    /// <summary>
    /// Year-over-year comparison
    /// </summary>
    public YearComparison Comparison { get; set; } = new();
}

/// <summary>
/// Legacy current statistics snapshot (prefer LiveStats)
/// </summary>
public class CurrentStats
{
    /// <summary>
    /// Total registered users
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Users online now
    /// </summary>
    public int OnlineUsers { get; set; }

    /// <summary>
    /// Total active games
    /// </summary>
    public int ActiveGames { get; set; }

    /// <summary>
    /// Total games (all statuses)
    /// </summary>
    public int TotalGames { get; set; }

    /// <summary>
    /// Total game posts
    /// </summary>
    public int TotalPosts { get; set; }

    /// <summary>
    /// Total forum topics
    /// </summary>
    public int TotalTopics { get; set; }
}

/// <summary>
/// Legacy statistics for a time period (prefer PeriodReport)
/// </summary>
public class PeriodStats
{
    /// <summary>
    /// New users registered
    /// </summary>
    public int NewUsers { get; set; }

    /// <summary>
    /// New games created
    /// </summary>
    public int NewGames { get; set; }

    /// <summary>
    /// New game posts
    /// </summary>
    public int NewPosts { get; set; }

    /// <summary>
    /// New forum topics
    /// </summary>
    public int NewTopics { get; set; }

    /// <summary>
    /// New forum comments
    /// </summary>
    public int NewComments { get; set; }
}

/// <summary>
/// Legacy year-over-year comparison (prefer PeriodComparison)
/// </summary>
public class YearComparison
{
    /// <summary>
    /// User growth percentage
    /// </summary>
    public double UsersGrowth { get; set; }

    /// <summary>
    /// Games growth percentage
    /// </summary>
    public double GamesGrowth { get; set; }

    /// <summary>
    /// Posts growth percentage
    /// </summary>
    public double PostsGrowth { get; set; }
}

#endregion
