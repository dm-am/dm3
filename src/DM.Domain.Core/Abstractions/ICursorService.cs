using System.Diagnostics.CodeAnalysis;
using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Abstractions;

/// <summary>
/// Service for encoding/decoding opaque pagination cursors
/// </summary>
public interface ICursorService
{
    /// <summary>
    /// Try to decode cursor string
    /// </summary>
    /// <param name="cursor">Opaque cursor string</param>
    /// <param name="data">Decoded cursor data, null when the cursor does not decode</param>
    /// <returns>True if successfully decoded</returns>
    bool TryDecode(string cursor, [NotNullWhen(true)] out CursorData? data);

    /// <summary>
    /// Create cursor for "after" pagination (fetch newer items)
    /// </summary>
    string CreateAfterCursor(Guid entityId, DateTimeOffset timestampUtc);

    /// <summary>
    /// Create cursor for "before" pagination (fetch older items)
    /// </summary>
    string CreateBeforeCursor(Guid entityId, DateTimeOffset timestampUtc);
}
