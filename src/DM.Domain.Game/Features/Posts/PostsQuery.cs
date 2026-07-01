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
    /// Filter posts whose last review was after this date
    /// </summary>
    public DateTimeOffset? LastReviewedAfter { get; set; }

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
    /// Minimum rating filter (inclusive, can be negative)
    /// </summary>
    public int? MinRating { get; set; }

    /// <summary>
    /// Maximum rating filter (inclusive, can be negative)
    /// </summary>
    public int? MaxRating { get; set; }

    /// <summary>
    /// Filter by post author usernames (comma-separated)
    /// </summary>
    public string? AuthorUsernames { get; set; }

    /// <summary>
    /// Filter to posts that have at least one review by this username.
    /// Зеркало <see cref="AuthorUsernames"/>, но смотрит «кто рецензировал»,
    /// а не «кто написал». Используется страницей профиля «Оценил чужих
    /// постов: {username}» (route given-reviews).
    /// </summary>
    public string? ReviewerUsername { get; set; }

    /// <summary>
    /// Filter posts created after this date
    /// </summary>
    public DateTimeOffset? CreatedAfter { get; set; }

    /// <summary>
    /// Filter posts created before this date
    /// </summary>
    public DateTimeOffset? CreatedBefore { get; set; }
}
