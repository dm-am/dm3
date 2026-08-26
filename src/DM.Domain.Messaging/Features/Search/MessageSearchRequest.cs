using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Search;

/// <summary>
/// Raw search request as received from the transport layer. Inline operator
/// tokens inside <see cref="Query"/> are parsed by the domain service.
/// </summary>
public class MessageSearchRequest
{
    /// <summary>Raw query string, may contain inline operators.</summary>
    public string Query { get; set; } = "";

    /// <summary>
    /// Structured, repeatable scope tokens: "global", "dm:&lt;id&gt;", "game:&lt;id&gt;".
    /// </summary>
    public IReadOnlyList<string> In { get; set; } = new List<string>();

    /// <summary>Optional explicit author username filter.</summary>
    public string? From { get; set; }

    /// <summary>Optional explicit inclusive lower bound on CreatedUtc.</summary>
    public DateTimeOffset? After { get; set; }

    /// <summary>Optional explicit inclusive upper bound on CreatedUtc.</summary>
    public DateTimeOffset? Before { get; set; }

    /// <summary>Opaque keyset cursor for the next (older) page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Requested page size.</summary>
    public int Limit { get; set; } = CursorQuery.DefaultLimit;
}
