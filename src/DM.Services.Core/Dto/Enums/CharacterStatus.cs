namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Character status (simplified to 4 values)
/// </summary>
public enum CharacterStatus
{
    /// <summary>
    /// The character requires GM review (was Registration)
    /// </summary>
    UnderReview = 0,

    /// <summary>
    /// GM declined the character
    /// </summary>
    Declined = 1,

    /// <summary>
    /// GM accepted the character and it is currently in the game
    /// </summary>
    Active = 2,

    /// <summary>
    /// Character is no longer active in the game (dead, left, or exiled)
    /// Check IsDead, IsPlayerLeft, IsPlayerExiled flags for details
    /// </summary>
    Retired = 3
}