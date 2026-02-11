using System;
using DM.Services.Core.Dto;

namespace DM.Services.Core.Implementation;

/// <summary>
/// Service for encoding/decoding opaque pagination cursors
/// </summary>
public interface ICursorService
{
    /// <summary>
    /// Encode cursor data into opaque string
    /// </summary>
    /// <param name="entityId">Entity ID</param>
    /// <param name="timestampUtc">Timestamp (UTC)</param>
    /// <param name="direction">Pagination direction</param>
    /// <returns>Opaque cursor string (base64 encoded)</returns>
    string Encode(Guid entityId, DateTimeOffset timestampUtc, CursorDirection direction);

    /// <summary>
    /// Decode opaque cursor string into cursor data
    /// </summary>
    /// <param name="cursor">Opaque cursor string</param>
    /// <returns>Decoded cursor data, or null if invalid</returns>
    CursorData? Decode(string cursor);

    /// <summary>
    /// Try to decode cursor string
    /// </summary>
    /// <param name="cursor">Opaque cursor string</param>
    /// <param name="data">Decoded cursor data</param>
    /// <returns>True if successfully decoded</returns>
    bool TryDecode(string cursor, out CursorData data);

    /// <summary>
    /// Create cursor for "after" pagination (fetch newer items)
    /// </summary>
    string CreateAfterCursor(Guid entityId, DateTimeOffset timestampUtc);

    /// <summary>
    /// Create cursor for "before" pagination (fetch older items)
    /// </summary>
    string CreateBeforeCursor(Guid entityId, DateTimeOffset timestampUtc);
}
