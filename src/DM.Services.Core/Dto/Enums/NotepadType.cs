namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Type of notepad
/// </summary>
public enum NotepadType
{
    /// <summary>
    /// Player's personal notes for a game
    /// </summary>
    Player = 1,

    /// <summary>
    /// Master/Assistant shared notes for a game
    /// </summary>
    Master = 2,

    /// <summary>
    /// Blog owner/assistant shared notes
    /// </summary>
    Blog = 3,

    /// <summary>
    /// User's private personal notes
    /// </summary>
    User = 4
}
