using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// What a reader asks of a page of one room's posts
/// </summary>
/// <remarks>
/// The same filters the rated feed (<see cref="PostsQuery"/>) takes, under the
/// same names, plus the two only a room has: the characters and posts written
/// without one. A post keeps its number in the room whatever the view: the
/// number is its place in the order the room is written in, not on the page.
/// </remarks>
public class RoomPostsQuery : PagingQuery
{
    /// <summary>
    /// Search by post text (case-insensitive contains)
    /// </summary>
    /// <remarks>
    /// The text a reader sees, not the body as written: the markup is not part of
    /// what is matched, and neither is anything the reader is not shown.
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
    /// Filter by post author usernames (repeat the parameter for several)
    /// </summary>
    public IReadOnlyCollection<string>? AuthorUsernames { get; set; }

    /// <summary>
    /// Filter by the characters the posts are written by (repeat the parameter for several)
    /// </summary>
    /// <remarks>
    /// Together with <see cref="WithoutCharacter"/> the choice is one set: posts by
    /// any of these characters or, when the flag is set, written without one.
    /// </remarks>
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
