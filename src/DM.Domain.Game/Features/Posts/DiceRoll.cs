using System;
using System.Collections.Generic;
using System.Linq;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Domain model for dice roll
/// </summary>
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
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Is appended after post creation
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
    /// Roll comment
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Individual die results
    /// </summary>
    public IEnumerable<DiceRollResult> Results { get; set; } = [];

    /// <summary>
    /// Total result (sum of all dice values plus bonus)
    /// </summary>
    public int Total => Results.Sum(r => r.Value) + Bonus;
}

/// <summary>
/// Domain model for a single die roll result
/// </summary>
public class DiceRollResult
{
    /// <summary>
    /// Die value
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Critical flag (natural max or min)
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Exploded flag (die rolled maximum value and triggered re-roll)
    /// </summary>
    public bool IsExploded { get; set; }
}
