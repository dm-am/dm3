using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <summary>
/// DTO model for game attribute schema
/// </summary>
public class AttributeSchema
{
    /// <summary>
    /// Schema identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Schema title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Schema author
    /// </summary>
    public User Author { get; set; } = null!;

    /// <summary>
    /// Schema type
    /// </summary>
    public SchemaType Type { get; set; }

    /// <summary>
    /// Schema specifications
    /// </summary>
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];
}
