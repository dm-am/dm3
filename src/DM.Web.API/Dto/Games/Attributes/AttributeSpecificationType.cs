namespace DM.Web.API.Dto.Games.Attributes;

/// <summary>
/// Specification type
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
    /// List of values with modifiers
    /// </summary>
    List = 2,

    /// <summary>
    /// BBCode formatted text (for appearance, personality, history, etc.)
    /// </summary>
    BbCode = 3
}