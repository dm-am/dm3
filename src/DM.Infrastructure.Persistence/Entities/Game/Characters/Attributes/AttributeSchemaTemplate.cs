using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// DAL model for a starting character sheet the site offers. The specifications
/// are an honest document - polymorphic constraints, nested value lists,
/// nothing ever queried from inside - and stay one jsonb column.
/// </summary>
/// <remarks>
/// Under the ordinary soft-delete filter, unlike the table it grew out of: that
/// exemption existed because a game reached its schema through a reference, and
/// a game carries a sheet of its own now. Nothing points at this row, so a
/// hidden template is simply gone to every reader.
/// </remarks>
[Table("AttributeSchemaTemplates")]
public class AttributeSchemaTemplate : IRemovable
{
    /// <summary>
    /// Template identifier
    /// </summary>
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Display name
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Attribute specifications
    /// </summary>
    public List<AttributeSpecification> Specifications { get; set; } = [];

    /// <summary>
    /// The one the creation form starts from
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Who put the template into the catalogue; emptied when that account goes,
    /// because the template belongs to the site and outlives its author
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// Moment the template entered the catalogue
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Moment of the last replacement
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }
}
