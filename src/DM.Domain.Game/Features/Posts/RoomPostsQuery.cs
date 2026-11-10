using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Posts;

/// <remarks>
/// The filters of <see cref="PostsQuery"/> under the same names, plus the characters. A post keeps its
/// number in the room whatever the view.
/// </remarks>
public class RoomPostsQuery : PagingQuery
{
    /// <remarks>
    /// The text a reader sees: neither the markup nor anything hidden from him is matched.
    /// </remarks>
    public string? Search { get; set; }

    /// <summary>
    /// Sort field: created (default, the order the room is written in), rating,
    /// reviewcount, lastreview
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort order: asc (default) or desc
    /// </summary>
    public string? SortOrder { get; set; }

    /// <summary>
    /// Repeat the parameter for several
    /// </summary>
    public IReadOnlyCollection<string>? AuthorUsernames { get; set; }

    /// <summary>
    /// With <see cref="WithoutCharacter"/> one set: posts by any of these or, with the flag, without one
    /// </summary>
    public IReadOnlyCollection<Guid>? CharacterIds { get; set; }

    /// <summary>
    /// Include posts written without a character; alone, only those
    /// </summary>
    public bool? WithoutCharacter { get; set; }

    /// <summary>
    /// Minimum rating filter (inclusive, can be negative)
    /// </summary>
    public int? MinRating { get; set; }

    /// <summary>
    /// Maximum rating filter (inclusive, can be negative)
    /// </summary>
    public int? MaxRating { get; set; }

    /// <summary>
    /// true: only posts with at least one review; false: only posts without reviews
    /// </summary>
    public bool? HasReviews { get; set; }

    /// <summary>
    /// Filter posts created at or after this date
    /// </summary>
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>
    /// Filter posts created at or before this date
    /// </summary>
    public DateTimeOffset? CreatedToUtc { get; set; }
}
