namespace DM.Domain.Core.Enums;

/// <summary>
/// Chat type
/// </summary>
public enum ChatType
{
    /// <summary>
    /// Direct message (1-on-1 chat between two users)
    /// </summary>
    Direct = 0,

    /// <summary>
    /// Group chat (multi-party private chat)
    /// </summary>
    Group = 1,

    /// <summary>
    /// Global chat (accessible to all authenticated users)
    /// </summary>
    Global = 2
}
