namespace DM.Domain.Core.Enums;

/// <summary>
/// Who may write in the discussion of a game. Reading is never part of it: the
/// discussion is read by everybody who may see the game, guests included.
/// </summary>
/// <remarks>
/// Value 2 stays undefined: a client that still sends it is refused by the
/// validators rather than handed a mode it did not ask for, and a discussion
/// closed to readers is not a setting the site has.
/// </remarks>
public enum CommentsAccessMode
{
    /// <summary>
    /// Everyone may write in the discussion
    /// </summary>
    Public = 0,

    /// <summary>
    /// Only those holding a role in the game may write, everybody else reads
    /// </summary>
    Readonly = 1
}
