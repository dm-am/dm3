using System;
using System.Collections.Generic;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Game.Dto.Shared;

/// <summary>
/// DTO model for attribute schema
/// </summary>
public class AttributeSchema
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
    /// Author (null for public system schemas)
    /// </summary>
    public GeneralUser? Author { get; set; }

    /// <summary>
    /// Schema access type
    /// </summary>
    public SchemaType Type { get; set; }

    /// <summary>
    /// Attribute specifications
    /// </summary>
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];
}