using System.Collections.Generic;

namespace DM.Services.Core.Dto;

/// <summary>
/// Result of cursor-based pagination
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public class CursorResult<T>
{
    /// <summary>
    /// Collection of entities
    /// </summary>
    public IEnumerable<T> Data { get; set; } = [];

    /// <summary>
    /// Cursor to fetch next page (newer items)
    /// Null if no more items in this direction
    /// </summary>
    public string? NextCursor { get; set; }

    /// <summary>
    /// Cursor to fetch previous page (older items)
    /// Null if no more items in this direction
    /// </summary>
    public string? PrevCursor { get; set; }

    /// <summary>
    /// Whether there are more items after this page
    /// </summary>
    public bool HasNext { get; set; }

    /// <summary>
    /// Whether there are more items before this page
    /// </summary>
    public bool HasPrev { get; set; }
}
