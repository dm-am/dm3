namespace DM.Domain.Core.Enums;

/// <remarks>
/// The three game notepads are separate types because they differ by who may open them.
/// </remarks>
public enum NotepadType
{
    /// <summary>
    /// A character's own notes, kept by the player who owns it and shown to
    /// nobody else - the game master included
    /// </summary>
    Player = 1,

    /// <summary>
    /// Notes of the game itself, shared by its master and assistants
    /// </summary>
    Master = 2,

    /// <summary>
    /// Blog owner/assistant shared notes
    /// </summary>
    Blog = 3,

    /// <summary>
    /// User's private personal notes
    /// </summary>
    User = 4,

    /// <summary>
    /// What the leads of a game keep about one character: shared by the master
    /// and the assistants, closed to the player who owns the character. Every
    /// character has one, not only an NPC
    /// </summary>
    CharacterMaster = 5
}
