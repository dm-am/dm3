using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Comments;

/// <summary>
/// What a reader asks of a page of comments, whichever discussion it is a page of.
/// </summary>
/// <remarks>
/// The binder's side of <see cref="ICommentsQuery" />, one class for all four discussions.
/// </remarks>
public class CommentsQuery : PagingQuery, ICommentsQuery
{
    /// <inheritdoc />
    public string? Search { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<string>? AuthorUsernames { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? CreatedToUtc { get; set; }

    /// <inheritdoc />
    public string? SortBy { get; set; }

    /// <inheritdoc />
    public string? SortOrder { get; set; }
}
