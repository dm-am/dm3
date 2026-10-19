using System.Collections.Generic;
using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

/// <summary>
/// The character sheet of one game: its specifications and the token that
/// decides whether a replacement is written or refused.
/// </summary>
public class GameAttributeSchema
{
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];

    /// <summary>
    /// Version of the stored document. Opaque to the caller, which only ever
    /// returns the number it was given.
    /// </summary>
    public int Version { get; set; }
}
