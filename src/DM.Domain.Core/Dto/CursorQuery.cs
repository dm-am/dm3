using System;
using System.Collections.Generic;

namespace DM.Domain.Core.Dto;

/// <summary>
/// Query parameters for cursor-based pagination
/// </summary>
public class CursorQuery
{
    /// <summary>
    /// Opaque cursor string (base64 encoded)
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Get messages around specific message ID
    /// </summary>
    public Guid? AroundEntityId { get; set; }

    /// <summary>
    /// Get messages nearest to specific timestamp (UTC)
    /// </summary>
    public DateTimeOffset? NearTimestampUtc { get; set; }

    /// <summary>
    /// Authors whose messages are left out of the page. Not a filter the caller
    /// chooses: the service fills it from the reader's personal blacklist when
    /// the reader switched on "Скрывать сообщения в чате" (HideMessages).
    /// </summary>
    public IReadOnlyCollection<Guid>? HiddenAuthorIds { get; set; }

    /// <summary>
    /// Messages created at or before this moment are left out of the page. Not a
    /// filter the caller chooses: the service fills it from the moment the reader
    /// cleared the history of the conversation for himself.
    /// </summary>
    public DateTimeOffset? HistoryClearedUtc { get; set; }

    /// <summary>
    /// Number of items to fetch (default: 50, max: 100)
    /// </summary>
    public int Limit { get; set; } = 50;

    /// <summary>
    /// Maximum allowed limit
    /// </summary>
    public const int MaxLimit = 100;

    /// <summary>
    /// Default limit
    /// </summary>
    public const int DefaultLimit = 50;

    /// <summary>
    /// Get effective limit (clamped to valid range)
    /// </summary>
    public int EffectiveLimit => Clamp(Limit);

    /// <summary>
    /// Clamp a requested page size into the valid range. The single formula:
    /// every paged query answers with the same bounds, whatever shape its own
    /// query object has.
    /// </summary>
    public static int Clamp(int limit) => Math.Clamp(limit, 1, MaxLimit);
}
