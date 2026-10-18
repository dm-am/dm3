using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

namespace DM.Infrastructure.Persistence.Entities.Game;

/// <summary>
/// DAL model for the character sheet of one game: the specifications its
/// characters are described by, and the token a replacement is checked against.
/// </summary>
/// <remarks>
/// Its own table rather than a column on the game, and keyed by the game so the
/// pair cannot come apart. The version belongs to the document alone: on the
/// game row it would have been bumped by a rename or a privacy switch too, and
/// a master saving a sheet nobody else touched would have been refused for it.
/// </remarks>
[Table("GameAttributeSchemas")]
public class GameAttributeSchema
{
    /// <summary>
    /// Game the sheet belongs to, and the identity of the row
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Attribute specifications
    /// </summary>
    public List<AttributeSpecification> Specifications { get; set; } = [];

    /// <summary>
    /// Grows by one on every replacement; a replacement carrying any other
    /// number touches nothing
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Moment of the last replacement
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
