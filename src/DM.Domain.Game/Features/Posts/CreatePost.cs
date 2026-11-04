using System;
using System.Collections.Generic;

namespace DM.Domain.Game.Features.Posts;

public class CreatePost
{
    public Guid RoomId { get; set; }

    public Guid? CharacterId { get; set; }

    /// <summary>
    /// In-character text
    /// </summary>
    public string GameText { get; set; } = null!;

    /// <summary>
    /// Out-of-character commentary
    /// </summary>
    public string? MetagameText { get; set; }

    /// <summary>
    /// Rolled by the server, and only when the room allows dice
    /// </summary>
    public IEnumerable<CreatePostDiceRoll> DiceRolls { get; set; } = [];
}

/// <summary>
/// The results come from <see cref="IDiceRoller"/>.
/// </summary>
public class CreatePostDiceRoll
{
    /// <summary>
    /// Y in XdY
    /// </summary>
    public int EdgesCount { get; set; }

    /// <summary>
    /// X in XdY
    /// </summary>
    public int DiceCount { get; set; } = 1;

    /// <summary>
    /// Added to the total
    /// </summary>
    public int Bonus { get; set; }

    /// <summary>
    /// Explosions per die on its maximum value; null or 0 does not explode
    /// </summary>
    public int? ExplosionCount { get; set; }

    /// <summary>
    /// Only the leads and the post author see it: the inverse of "показывать всем результаты"
    /// </summary>
    public bool IsHidden { get; set; }

    public string? Comment { get; set; }
}
