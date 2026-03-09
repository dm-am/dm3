using System.Text.Json.Serialization;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Offset-based pagination info in API response
/// </summary>
/// <remarks>
/// Contains information about pagination state for paginated responses.
/// Uses skip/take pattern per API standard.
/// </remarks>
public class PagingInfo
{
    /// <summary>
    /// Creates pagination info from service result
    /// </summary>
    public PagingInfo(PagingResult pagingResult)
    {
        Skip = (pagingResult.CurrentPage - 1) * pagingResult.PageSize;
        Take = pagingResult.PageSize;
        Total = pagingResult.TotalEntitiesCount;
    }

    /// <summary>
    /// Creates pagination info with explicit values
    /// </summary>
    public PagingInfo(int skip, int take, int total)
    {
        Skip = skip;
        Take = take;
        Total = total;
    }

    /// <summary>
    /// Number of items skipped
    /// </summary>
    public int Skip { get; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int Take { get; }

    /// <summary>
    /// Total item count
    /// </summary>
    public int Total { get; }
}

/// <summary>
/// Cursor-based pagination info in API response
/// </summary>
/// <remarks>
/// Used for real-time data and infinite scroll patterns.
/// </remarks>
public class CursorPagingInfo
{
    /// <summary>
    /// Creates cursor pagination info
    /// </summary>
    public CursorPagingInfo(string? nextCursor, bool hasMore)
    {
        NextCursor = nextCursor;
        HasMore = hasMore;
    }

    /// <summary>
    /// Creates cursor pagination info for bidirectional navigation
    /// </summary>
    public CursorPagingInfo(string? nextCursor, string? previousCursor, bool hasMoreAfter, bool hasMoreBefore)
    {
        NextCursor = nextCursor;
        PreviousCursor = previousCursor;
        HasMore = hasMoreAfter;
        HasMoreBefore = hasMoreBefore;
    }

    /// <summary>
    /// Cursor for next page (if hasMore is true)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextCursor { get; }

    /// <summary>
    /// Cursor for previous page (optional, for bidirectional)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousCursor { get; }

    /// <summary>
    /// Whether there are more items after current set
    /// </summary>
    public bool HasMore { get; }

    /// <summary>
    /// Whether there are more items before current set (optional)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HasMoreBefore { get; }
}
