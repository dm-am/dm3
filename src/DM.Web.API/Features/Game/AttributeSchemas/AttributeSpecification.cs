using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <summary>
/// DTO model for attribute specification
/// </summary>
public class AttributeSpecification
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Value is required
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Constraints type
    /// </summary>
    public AttributeSpecificationType Type { get; set; }

    /// <summary>
    /// Order index within the schema
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Maximal length for string/number/BBCode constraints
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// List of possible values for list constraints
    /// </summary>
    public IEnumerable<AttributeValueSpecification> Values { get; set; } = [];

    /// <summary>
    /// Show on game main page as descriptor
    /// </summary>
    public bool IsDescriptor { get; set; }

    /// <summary>
    /// Hide from other players (only GM and owner can see)
    /// </summary>
    public bool IsHidden { get; set; }
}
