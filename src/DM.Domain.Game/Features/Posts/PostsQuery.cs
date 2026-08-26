using System;
using System.Collections.Generic;
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
    /// Filter posts whose last review was at or after this date
    /// </summary>
    public DateTimeOffset? LastReviewedFromUtc { get; set; }

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
    /// <remarks>
    /// The text a reader sees, not the body as written: the markup is not part of
    /// what is matched, and neither is anything the reader is not shown.
    /// </remarks>
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
    /// Filter by post author usernames (repeat the parameter for several)
    /// </summary>
    /// <remarks>
    /// Plural because it is repeatable, per the query vocabulary in
    /// API_DESIGN.md. It used to be one string holding a list under the same
    /// plural name that /v1/games spells as a repeated parameter, so the same
    /// name meant two different encodings on two neighbouring endpoints.
    /// </remarks>
    public IReadOnlyCollection<string>? AuthorUsernames { get; set; }

    /// <summary>
    /// Filter to posts that have at least one review by this username.
    /// Mirror of <see cref="AuthorUsernames"/>, but looks at "who reviewed"
    /// rather than "who wrote". Used by the profile page "Оценил чужих
    /// постов: {username}" (route given-reviews).
    /// </summary>
    public string? ReviewerUsername { get; set; }

    /// <summary>
    /// Filter posts created at or after this date
    /// </summary>
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>
    /// Filter posts created at or before this date
    /// </summary>
    public DateTimeOffset? CreatedToUtc { get; set; }
}
