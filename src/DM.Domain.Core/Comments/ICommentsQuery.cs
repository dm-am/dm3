using System;
using System.Collections.Generic;

namespace DM.Domain.Core.Comments;

/// <summary>
/// What a reader asks of a page of comments, whichever discussion it is a page of.
/// </summary>
/// <remarks>
/// Forum topics, games, blogs and publications keep their comments in one table and
/// filter them by the same six questions, so each module's query object answers those
/// six under its own name. The contract is what lets the filtering itself be written
/// once: without it the storage layer cannot name the shape it reads, and the block
/// gets copied per module instead — which is exactly how the four copies came about.
/// Read-only, because a query object is filled in by the binder and read from here.
/// </remarks>
public interface ICommentsQuery
{
    /// <summary>
    /// Text search by comment content (case-insensitive contains)
    /// </summary>
    string? Search { get; }

    /// <summary>
    /// Filter by author usernames (case-insensitive, OR logic)
    /// </summary>
    IReadOnlyCollection<string>? AuthorUsernames { get; }

    /// <summary>
    /// Filter by created date (from)
    /// </summary>
    DateTimeOffset? CreatedFromUtc { get; }

    /// <summary>
    /// Filter by created date (to)
    /// </summary>
    DateTimeOffset? CreatedToUtc { get; }

    /// <summary>
    /// Sort field: created (default), likes
    /// </summary>
    string? SortBy { get; }

    /// <summary>
    /// Sort order: asc (default for created) or desc
    /// </summary>
    string? SortOrder { get; }
}
