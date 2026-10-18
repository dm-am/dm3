using System.Collections.Generic;
using DM.Web.API.Features.Game.AttributeSchemas;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// API DTO for the character sheet of a game
/// </summary>
public class GameAttributeSchema
{
    /// <summary>
    /// Attribute specifications
    /// </summary>
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];

    /// <summary>
    /// Version of the stored sheet. A number without meaning: the caller
    /// compares it to nothing and only sends back the one it was given.
    /// </summary>
    public int Version { get; set; }
}
