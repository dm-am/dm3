using System.Collections.Generic;

namespace DM.Web.API.Dto.Contracts;

/// <summary>
/// Enveloped list DTO model with cursor-based pagination
/// </summary>
/// <typeparam name="T">Enveloped type</typeparam>
public class CursorEnvelope<T>
{
    /// <inheritdoc />
    public CursorEnvelope(IEnumerable<T> resources, CursorPaging paging)
    {
        Resources = resources;
        Paging = paging;
    }

    /// <summary>
    /// Enveloped resources
    /// </summary>
    public IEnumerable<T> Resources { get; }

    /// <summary>
    /// Cursor-based paging data
    /// </summary>
    public CursorPaging Paging { get; }
}

/// <summary>
/// Cursor-based pagination information
/// </summary>
public class CursorPaging
{
    /// <summary>
    /// Cursor for the next page (newer messages)
    /// </summary>
    public string? NextCursor { get; set; }

    /// <summary>
    /// Cursor for the previous page (older messages)
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
