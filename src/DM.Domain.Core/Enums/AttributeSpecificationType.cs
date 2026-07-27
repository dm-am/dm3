namespace DM.Domain.Core.Enums;

/// <summary>
/// Specification constraints type
/// </summary>
public enum AttributeSpecificationType
{
    /// <summary>
    /// Text
    /// </summary>
    Text = 0,

    /// <summary>
    /// Number
    /// </summary>
    Number = 1,

    /// <summary>
    /// List of text values
    /// </summary>
    TextList = 2,

    /// <summary>
    /// List of number values
    /// </summary>
    NumberList = 3,

    /// <summary>
    /// List of text and number values
    /// </summary>
    TextNumberList = 4,

    /// <summary>
    /// BBCode formatted text (for appearance, personality, history, etc.)
    /// </summary>
    BbCode = 5
}
