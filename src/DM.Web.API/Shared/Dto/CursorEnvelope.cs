using System.Collections.Generic;

namespace DM.Web.API.Shared.Dto;

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
/// <remarks>
/// Next and prev follow the order the endpoint itself sorts by, not a fixed
/// direction in time, and the cursor value is opaque - it is produced by one
/// endpoint and handed back to the same one. Which order that is belongs to the
/// endpoint and is documented there.
/// </remarks>
public class CursorPaging
{
    /// <summary>
    /// Cursor for the next page in the order the endpoint sorts by
    /// </summary>
    public string? NextCursor { get; set; }

    /// <summary>
    /// Cursor for the previous page in the order the endpoint sorts by
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
