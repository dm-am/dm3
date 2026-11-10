using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Posts;

public class PostsQuery : PagingQuery
{
    public Guid? GameId { get; set; }

    /// <summary>
    /// Filter posts whose last review was at or after this date
    /// </summary>
    public DateTimeOffset? LastReviewedFromUtc { get; set; }

    /// <summary>
    /// true: only posts with at least one review; false: only posts without reviews
    /// </summary>
    public bool? HasReviews { get; set; }

    /// <summary>
    /// Sort field: rating (default), created, lastreview, reviewcount
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort order: asc or desc (default: desc)
    /// </summary>
    public string? SortOrder { get; set; }

    /// <remarks>
    /// The text a reader sees: neither the markup nor anything hidden from him is matched.
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
    /// Repeat the parameter for several
    /// </summary>
    public IReadOnlyCollection<string>? AuthorUsernames { get; set; }

    /// <summary>
    /// Posts with at least one review by this user: who reviewed, not who wrote
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
