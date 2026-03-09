using System;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// DAL model for attribute specification
/// </summary>
public class AttributeSpecification
{
    /// <summary>
    /// Specification identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Specification constraints
    /// </summary>
    public AttributeConstraints Constraints { get; set; } = null!;

    /// <summary>
    /// Show on game main page as descriptor
    /// </summary>
    public bool IsDescriptor { get; set; }

    /// <summary>
    /// Hide from other players (only GM and owner can see)
    /// </summary>
    public bool IsHidden { get; set; }
}
