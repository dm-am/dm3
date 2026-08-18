namespace DM.Domain.Core.Enums;

/// <summary>
/// Type of notepad
/// </summary>
/// <remarks>
/// Three of the five hang off a game and differ by who may open them, which is
/// the whole reason they are separate types rather than one notepad with a
/// role check bolted on: <see cref="Master" /> belongs to the game and is read
/// by its leads, <see cref="Player" /> belongs to a character and is read by
/// its owner alone, <see cref="CharacterMaster" /> belongs to the same
/// character and is read by the leads alone. On screen they are "Заметки
/// игры", "Заметки игрока" and "Заметки мастера".
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
