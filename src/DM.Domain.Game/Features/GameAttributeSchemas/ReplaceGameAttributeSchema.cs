using System;
using System.Collections.Generic;
using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

public class ReplaceGameAttributeSchema
{
    public Guid GameId { get; set; }

    /// <summary>
    /// The whole new set. A specification keeps its identifier only when the
    /// stored set already holds it; every other identifier is replaced.
    /// </summary>
    public IEnumerable<UpdateAttributeSpecification> Specifications { get; set; } = [];

    /// <summary>
    /// The version the caller read the sheet at
    /// </summary>
    public int? Version { get; set; }
}
