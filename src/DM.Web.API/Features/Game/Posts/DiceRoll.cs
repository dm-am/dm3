using System.Collections.Generic;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// DTO model for dice roll
/// </summary>
public class DiceRoll
{
    /// <summary>
    /// Rolls count
    /// </summary>
    public int Rolls { get; set; }

    /// <summary>
    /// Die edges count
    /// </summary>
    public int Edges { get; set; }

    /// <summary>
    /// Additional bonus
    /// </summary>
    public int Bonus { get; set; }

    /// <summary>
    /// Explosion count (max re-rolls when die shows maximum value)
    /// </summary>
    public int? Explosion { get; set; }

    /// <summary>
    /// Commentary
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Results
    /// </summary>
    public IEnumerable<DiceResult> Results { get; set; } = [];
}

/// <summary>
/// DTO model for dice roll result
/// </summary>
public class DiceResult
{
    /// <summary>
    /// Roll result value
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Result was critical
    /// </summary>
    public bool Critical { get; set; }

    /// <summary>
    /// Result has exploded (triggered re-roll)
    /// </summary>
    public bool Exploded { get; set; }
}
