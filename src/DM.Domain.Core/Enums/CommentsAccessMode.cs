using System.ComponentModel;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Game comments access mode
/// </summary>
public enum CommentsAccessMode
{
    /// <summary>
    /// Everyone may read and write comments in the game
    /// </summary>
    [Description("Доступно всем без ограничений")]
    Public = 0,

    /// <summary>
    /// Everyone may read, but only game players may write comments
    /// </summary>
    [Description("Доступно посторонним только для чтения")]
    Readonly = 1,

    /// <summary>
    /// Only game players may read or write comments
    /// </summary>
    [Description("Недоступно посторонним")]
    Private = 2
}
