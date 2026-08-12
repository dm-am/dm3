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
    /// Global chat (public: reading is open to guests, posting requires authentication)
    /// </summary>
    Global = 2,

    /// <summary>
    /// Game room chat (linked to a game room for player communication)
    /// </summary>
    GameRoom = 3
}
