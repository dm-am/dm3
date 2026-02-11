using System;

namespace DM.Services.Game.Dto.Output;

/// <summary>
/// DTO model for character attribute
/// </summary>
public class CharacterAttribute
{
    /// <summary>
    /// Specification identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Specification title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Attribute value
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Attribute modifier
    /// </summary>
    public int? Modifier { get; set; }

    /// <summary>
    /// Flag of consistency
    /// </summary>
    public bool Inconsistent { get; set; }
}