namespace DM.Domain.Game.Authorization;

public enum CharacterIntention
{
    Edit = 1,

    EditPrivacySettings = 2,

    EditMasterSettings = 3,

    Delete = 4,

    Accept = 5,

    Decline = 6,

    Kill = 7,

    Resurrect = 8,

    Leave = 9,

    Exile = 15,

    /// <summary>
    /// Bring an exiled character back into the game
    /// </summary>
    Readmit = 16
}
