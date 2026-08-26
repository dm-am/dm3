using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// DAL model for attribute schema. The specifications are an honest document —
/// polymorphic constraints, nested value lists, nothing ever queried from
/// inside — and stay one jsonb column.
/// </summary>
/// <remarks>
/// Deliberately excluded from the global soft-delete query filter: a removed
/// schema is hidden from the lists a user picks one from, while a game already
/// built on it keeps resolving it through its own reference — each read spells
/// its own IsRemoved predicate.
/// </remarks>
[Table("AttributeSchemata")]
public class AttributeSchema : IRemovable
{
    /// <summary>
    /// Schema identifier
    /// </summary>
    public Guid AttributeSchemaId { get; set; }

    /// <summary>
    /// Schema author and owner
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Schema access type
    /// </summary>
    public SchemaType Type { get; set; }

    /// <summary>
    /// Display name
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Attribute specifications
    /// </summary>
    public List<AttributeSpecification> Specifications { get; set; } = [];

    /// <inheritdoc />
    public bool IsRemoved { get; set; }
}
