using System;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Infrastructure.Persistence.Entities.Game.Posts;

/// <summary>
/// DAL model for dice roll
/// </summary>
[MongoCollectionName("Dice")]
public class DiceRoll
{
    /// <summary>
    /// Roll identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Is appended flag
    /// </summary>
    public bool IsAdditional { get; set; }

    /// <summary>
    /// Only GM and post author can see hidden rolls
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Fair roll result can only be seen after the post was created
    /// </summary>
    public bool IsFair { get; set; }

    /// <summary>
    /// Number of dice (X in XdY)
    /// </summary>
    public int DiceCount { get; set; }

    /// <summary>
    /// Number of single die edges (Y in XdY)
    /// </summary>
    public int EdgesCount { get; set; }

    /// <summary>
    /// Maximum number of dice explosions (no explosion if 0, unlimited explosions if null)
    /// </summary>
    public int? ExplosionCount { get; set; }

    /// <summary>
    /// Constant bonus
    /// </summary>
    public int Bonus { get; set; }

    /// <summary>
    /// Roll comment. The stored element keeps its lowercase name: the driver maps
    /// by member name unless told otherwise, so dropping this mapping would read
    /// back an empty comment on every existing document without an error.
    /// </summary>
    [BsonElement("comment")]
    public string Comment { get; set; } = null!;

    /// <summary>
    /// Result
    /// </summary>
    public RollResult[] Result { get; set; } = null!;
}

/// <summary>
/// DAL model for a single die roll result
/// </summary>
public class RollResult
{
    /// <summary>
    /// Die value
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Critical flag
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Exploded flag (die rolled maximum value and triggered re-roll)
    /// </summary>
    public bool IsExploded { get; set; }
}
