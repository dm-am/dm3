using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Comments;

/// <summary>
/// What a reader asks of a page of comments, whichever discussion it is a page of.
/// </summary>
/// <remarks>
/// The four discussions keep comments in one table, so the binder and the storage
/// layer share this one class.
/// </remarks>
public class CommentsQuery : PagingQuery
{
    /// <summary>
    /// Text search by comment content (case-insensitive contains)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by author usernames (case-insensitive, OR logic)
    /// </summary>
    public IReadOnlyCollection<string>? AuthorUsernames { get; set; }

    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>
    /// Filter by created date (to)
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
