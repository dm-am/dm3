namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// Kind of values held by a list attribute
/// </summary>
public enum ListValueKind
{
    /// <summary>
    /// Text values
    /// </summary>
    Text = 0,

    /// <summary>
    /// Number values
    /// </summary>
    Number = 1,

    /// <summary>
    /// Text and number values
    /// </summary>
    TextNumber = 2
}
