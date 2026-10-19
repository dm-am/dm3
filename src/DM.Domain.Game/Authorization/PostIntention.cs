namespace DM.Domain.Game.Authorization;

public enum PostIntention
{
    /// <summary>
    /// Change GameText and MetagameText
    /// </summary>
    EditText = 1,

    EditCharacter = 2,

    Delete = 4
}
