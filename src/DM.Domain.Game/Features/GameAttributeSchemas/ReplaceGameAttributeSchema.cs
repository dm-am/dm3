using System;
using System.Collections.Generic;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

/// <summary>
/// DTO for replacing the character sheet of a game
/// </summary>
public class ReplaceGameAttributeSchema
{
    /// <summary>
    /// Game whose sheet is being replaced
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// The whole new set. A specification keeps its identifier only when the
    /// stored set already holds it; every other identifier is replaced.
    /// </summary>
    public IEnumerable<UpdateAttributeSpecification> Specifications { get; set; } = [];

    /// <summary>
    /// The version the caller read the sheet at. Absent is a caller that never
    /// read one, which is refused rather than treated as "whatever is there".
    /// </summary>
    public int? Version { get; set; }
}
