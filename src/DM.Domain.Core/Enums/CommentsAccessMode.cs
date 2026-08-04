namespace DM.Domain.Core.Enums;

/// <summary>
/// Game comments access mode
/// </summary>
public enum CommentsAccessMode
{
    /// <summary>
    /// Everyone may read and write comments in the game
    /// </summary>
    Public = 0,

    /// <summary>
    /// Everyone may read, but only game players may write comments
    /// </summary>
    Readonly = 1,

    /// <summary>
    /// Only game players may read or write comments
    /// </summary>
    Private = 2
}
