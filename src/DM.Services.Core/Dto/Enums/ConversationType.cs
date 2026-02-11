namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Conversation type
/// </summary>
public enum ConversationType
{
    /// <summary>
    /// Direct message (1-on-1 conversation between two users)
    /// </summary>
    Direct = 0,

    /// <summary>
    /// Group conversation (multi-party private conversation)
    /// </summary>
    Group = 1,

    /// <summary>
    /// Global chat (accessible to all authenticated users)
    /// </summary>
    Global = 2
}
