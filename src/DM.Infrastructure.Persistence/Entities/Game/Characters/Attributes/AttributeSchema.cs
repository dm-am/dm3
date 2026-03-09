using System;
using System.Collections.Generic;
using DM.Infrastructure.Persistence.Entities.DataContracts;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// DAL model for attribute schema
/// </summary>
[MongoCollectionName("AttributeSchemata")]
public class AttributeSchema : IRemovable
{
    /// <summary>
    /// Schema identifier
    /// </summary>
    public Guid Id { get; set; }

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
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];

    /// <inheritdoc />
    public bool IsRemoved { get; set; }
}
