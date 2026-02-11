using System;

namespace DM.Services.Core.Dto;

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
    public int EffectiveLimit => Math.Clamp(Limit, 1, MaxLimit);
}
