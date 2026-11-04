using System;
using System.Collections.Generic;
using System.Linq;

namespace DM.Domain.Game.Features.Posts;

public class DiceRoll
{
    public Guid Id { get; set; }

    public Guid PostId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Only the leads and the post author see a hidden roll
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// The room shows dice results to every reader
    /// </summary>
    public bool RoomViewDiceResults { get; set; }

    /// <summary>
    /// The game shows dice results to its leads and the post author only
    /// </summary>
    public bool GameHideDiceResult { get; set; }

    /// <summary>
    /// X in XdY
    /// </summary>
    public int DiceCount { get; set; }

    /// <summary>
    /// Y in XdY
    /// </summary>
    public int EdgesCount { get; set; }

    /// <summary>
    /// 0 does not explode, null is unlimited
    /// </summary>
    public int? ExplosionCount { get; set; }

    public int Bonus { get; set; }

    public string Comment { get; set; } = string.Empty;

    public IEnumerable<DiceRollResult> Results { get; set; } = [];

    public int Total => Results.Sum(r => r.Value) + Bonus;
}

public class DiceRollResult
{
    public int Value { get; set; }

    /// <summary>
    /// Natural maximum or minimum
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Rolled its maximum and triggered a re-roll
    /// </summary>
    public bool IsExploded { get; set; }
}
