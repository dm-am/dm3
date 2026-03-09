using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// DAL model for attribute value
/// </summary>
[Table("CharacterAttributes")]
public class CharacterAttribute
{
    /// <summary>
    /// Value identifier
    /// </summary>
    [Key]
    public Guid CharacterAttributeId { get; set; }

    /// <summary>
    /// Attribute specification identifier
    /// </summary>
    public Guid AttributeId { get; set; }

    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Attribute value
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Character
    /// </summary>
    [ForeignKey(nameof(CharacterId))]
    public virtual Character Character { get; set; } = null!;
}
