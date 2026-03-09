namespace DM.Domain.Core.Enums;

/// <summary>
/// Game rubric access mode
/// </summary>
public enum RubricAccessType
{
    /// <summary>
    /// Anyone can view the rubric
    /// </summary>
    Open = 0,

    /// <summary>
    /// Only users with explicit access may view the rubric
    /// </summary>
    Private = 1
}
