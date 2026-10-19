using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.AttributeSchemas;

public class AttributeSpecification
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public int Order { get; set; }

    public AttributeSpecificationType Type { get; set; }

    public bool Required { get; set; }

    /// <summary>
    /// For text, number and BBCode types
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// For list types
    /// </summary>
    public IEnumerable<ListValue> Values { get; set; } = [];

    public bool IsDescriptor { get; set; }

    public bool IsHidden { get; set; }
}

public class ListValue
{
    public string Value { get; set; } = null!;

    public int? Modifier { get; set; }
}

/// <summary>
/// Shape of an attribute specification on the write path
/// </summary>
public class CreateAttributeSpecification
{
    public string Title { get; set; } = null!;

    public AttributeSpecificationType Type { get; set; }

    public bool Required { get; set; }

    /// <summary>
    /// Shown on the game page
    /// </summary>
    public bool IsDescriptor { get; set; }

    /// <summary>
    /// Only the leads and the owner see its value
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// For text, number and BBCode types
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// For list types
    /// </summary>
    public IEnumerable<ListValue> Values { get; set; } = [];

    public int Order { get; set; }
}

public class UpdateAttributeSpecification : CreateAttributeSpecification
{
    /// <summary>
    /// Null for a new specification
    /// </summary>
    public Guid? Id { get; set; }
}
