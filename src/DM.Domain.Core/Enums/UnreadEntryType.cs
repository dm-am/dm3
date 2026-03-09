namespace DM.Domain.Core.Enums;

/// <summary>
/// Type of possibly unread entry
/// </summary>
public enum UnreadEntryType
{
    /// <summary>
    /// Common text messages, such as posts, comments, private messages etc.
    /// </summary>
    Message = 0,

    /// <summary>
    /// Game characters (for GM only)
    /// </summary>
    Character = 1
}
