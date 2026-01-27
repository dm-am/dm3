namespace DM.Web.API.Dto.Community;

/// <summary>
/// Community statistics response
/// </summary>
public class CommunityStats
{
    /// <summary>
    /// Current statistics
    /// </summary>
    public CurrentStats Current { get; set; }

    /// <summary>
    /// Monthly statistics
    /// </summary>
    public PeriodStats Monthly { get; set; }

    /// <summary>
    /// Yearly statistics
    /// </summary>
    public PeriodStats Yearly { get; set; }

    /// <summary>
    /// Year-over-year comparison
    /// </summary>
    public YearComparison Comparison { get; set; }
}

/// <summary>
/// Current statistics snapshot
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
/// Statistics for a time period
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
/// Year-over-year comparison
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
