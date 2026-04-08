using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Query parameters for global post list filtering
/// </summary>
public class PostsQuery : PagingQuery
{
    /// <summary>
    /// Filter by game ID
    /// </summary>
    public Guid? GameId { get; set; }

    /// <summary>
    /// Filter posts that received reviews after this date
    /// </summary>
    public DateTimeOffset? ReviewedAfter { get; set; }

    /// <summary>
    /// Only include posts with at least one review
    /// </summary>
    public bool? HasReviews { get; set; }

    /// <summary>
    /// Sort field: created, rating, lastreview
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort order: asc or desc (default: desc)
    /// </summary>
    public string? SortOrder { get; set; }

    /// <summary>
    /// Search by post text (case-insensitive contains)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Minimum rating filter (inclusive)
    /// </summary>
    public int? MinRating { get; set; }
}
