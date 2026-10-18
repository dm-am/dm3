namespace DM.Domain.Game.Authorization;

/// <summary>
/// List of character intentions that requires authorization
/// </summary>
public enum CharacterIntention
{
    /// <summary>
    /// Edit character general information
    /// </summary>
    Edit = 1,

    /// <summary>
    /// Edit character privacy settings
    /// </summary>
    EditPrivacySettings = 2,

    /// <summary>
    /// Edit character master settings
    /// </summary>
    EditMasterSettings = 3,

    /// <summary>
    /// Delete character
    /// </summary>
    Delete = 4,

    /// <summary>
    /// Accept the character
    /// </summary>
    Accept = 5,

    /// <summary>
    /// Decline the character
    /// </summary>
    Decline = 6,

    /// <summary>
    /// Kill the character
    /// </summary>
    Kill = 7,

    /// <summary>
    /// Resurrect the character
    /// </summary>
    Resurrect = 8,

    /// <summary>
    /// Leave the game
    /// </summary>
    Leave = 9,

    // Slot 10 was Return, the player bringing back a character he took out of
    // the game himself. Nobody brings such a character back: leaving is final,
    // and a lead undoes only his own moves (Accept after Decline, Resurrect after
    // Kill, Readmit after Exile).

    /// <summary>
    /// Exile the character from the game
    /// </summary>
    Exile = 15,

    /// <summary>
    /// Bring an exiled character back into the game
    /// </summary>
    Readmit = 16
}
