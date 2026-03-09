using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Dto;

/// <summary>
/// Internal cursor data for encoding/decoding opaque cursors
/// </summary>
public class CursorData
{
    /// <summary>
    /// Entity ID at the cursor position
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Timestamp at the cursor position (UTC)
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>
    /// Direction for fetching relative to cursor
    /// </summary>
    public CursorDirection Direction { get; set; }
}
