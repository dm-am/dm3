namespace DM.Domain.Core.Comments;

/// <summary>
/// What a reader asks of a page of comments, whichever discussion it is a page of.
/// </summary>
/// <remarks>
/// The four discussions keep comments in one table, so the storage layer filters
/// through this one read-only contract.
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
