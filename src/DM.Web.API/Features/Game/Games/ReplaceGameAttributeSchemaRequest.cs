using System.Collections.Generic;
using DM.Web.API.Features.Game.AttributeSchemas;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Replacement of the whole character sheet of a game
/// </summary>
/// <remarks>
/// The whole set every time: a sheet is what the game's characters are
/// described by, and a partial edit of it has no meaning the server could act
/// on. A specification keeps its identifier when the stored sheet already holds
/// it, which is how the values written against it survive the save.
/// </remarks>
public class ReplaceGameAttributeSchemaRequest
{
    /// <summary>
    /// The new set of specifications. An empty one is a game whose characters
    /// carry no attributes.
    /// </summary>
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];

    /// <summary>
    /// The version the sheet was read at. Absent is refused: this write deletes
    /// the values of every specification it drops.
    /// </summary>
    public int? Version { get; set; }
}
