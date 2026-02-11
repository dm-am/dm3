namespace DM.Services.Game.Dto.Shared;

/// <summary>
/// Specification constraints type
/// </summary>
public enum AttributeSpecificationType
{
    /// <summary>
    /// Number
    /// </summary>
    Number = 0,

    /// <summary>
    /// String
    /// </summary>
    String = 1,

    /// <summary>
    /// List
    /// </summary>
    List = 2,

    /// <summary>
    /// BBCode formatted text (for appearance, personality, history, etc.)
    /// </summary>
    BbCode = 3
}