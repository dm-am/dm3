using System;
using System.Collections.Generic;
using DM.Web.API.Features.Game.AttributeSchemas;

namespace DM.Web.API.Features.Game.AttributeSchemaTemplates;

/// <summary>
/// DTO model for a starting character sheet the site offers
/// </summary>
public class AttributeSchemaTemplate
{
    /// <summary>
    /// Template identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Whether the creation form starts from this template. Read-only: it is
    /// moved by an action of its own.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Attribute specifications
    /// </summary>
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];
}
