namespace DM.Domain.Core.Enums;

/// <summary>
/// Direction for cursor-based pagination
/// </summary>
public enum CursorDirection
{
    /// <summary>
    /// Get messages after the cursor (newer)
    /// </summary>
    After = 0,

    /// <summary>
    /// Get messages before the cursor (older)
    /// </summary>
    Before = 1
}
