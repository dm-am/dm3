using System;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Featured/highlighted post for display in various contexts
/// </summary>
/// <remarks>
/// Used in:
/// - User profiles (best post by rating)
/// - Homepage (weekly best, last reviewed)
/// - Game pages (featured posts)
///
/// Fields are nullable based on context:
/// - AuthorUsername: null in user profile (author is the profile owner)
/// - Text, RoomId, RoomTitle, ReviewCount, CreatedUtc: null in compact views (homepage)
/// </remarks>
public class FeaturedPost
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Post rating (sum of review scores)
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = string.Empty;

    /// <summary>
    /// Room identifier
    /// </summary>
    /// <remarks>
    /// Null in compact views (homepage highlights)
    /// </remarks>
    public Guid? RoomId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    /// <remarks>
    /// Null in compact views (homepage highlights)
    /// </remarks>
    public string? RoomTitle { get; set; }

    /// <summary>
    /// Post author username
    /// </summary>
    /// <remarks>
    /// Null when context implies the author (e.g., user profile page)
    /// </remarks>
    public string? AuthorUsername { get; set; }

    /// <summary>
    /// Post text preview (BB-code rendered)
    /// </summary>
    /// <remarks>
    /// Null in compact views (homepage highlights)
    /// </remarks>
    public string? Text { get; set; }

    /// <summary>
    /// Number of reviews received
    /// </summary>
    /// <remarks>
    /// Null in compact views
    /// </remarks>
    public int? ReviewCount { get; set; }

    /// <summary>
    /// Post creation date (UTC)
    /// </summary>
    /// <remarks>
    /// Null in compact views
    /// </remarks>
    public DateTimeOffset? CreatedUtc { get; set; }
}
