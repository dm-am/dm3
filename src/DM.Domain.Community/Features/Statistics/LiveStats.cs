using System;

namespace DM.Domain.Community.Features.Statistics;

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
