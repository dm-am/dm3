using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Comments;

/// <summary>
/// What a reader asks of a page of comments, whichever discussion it is a page of.
/// </summary>
/// <remarks>
/// The binder's side of <see cref="ICommentsQuery" />. Four modules held a copy of
/// this class apiece, identical down to the doc comments, and the two tiers around
/// them had already stopped copying: the storage layer filters through the
/// interface, and the client states the same six fields once. A per-module class
/// bought nothing but the chance for one of the four to drift — a sort key added
/// here and missing there reads to the user as a discussion where sorting is
/// broken.
/// </remarks>
public class CommentsQuery : PagingQuery, ICommentsQuery
{
    /// <summary>
    /// Text search by comment content (case-insensitive contains)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by author usernames (case-insensitive, OR logic)
    /// </summary>
    public IReadOnlyCollection<string>? AuthorUsernames { get; set; }

    /// <summary>
    /// Filter by created date (from) - ISO 8601 format
    /// </summary>
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>
    /// Filter by created date (to) - ISO 8601 format
    /// </summary>
    public DateTimeOffset? CreatedToUtc { get; set; }

    /// <summary>
    /// Sort field: created (default), likes
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort order: asc (default for created) or desc
    /// </summary>
    public string? SortOrder { get; set; }
}
