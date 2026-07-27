using System;
using System.Collections.Generic;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// DTO model for post creating
/// </summary>
public class CreatePost
{
    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Game text (in-character content)
    /// </summary>
    public string GameText { get; set; } = null!;

    /// <summary>
    /// Metagame text (OOC commentary)
    /// </summary>
    public string? MetagameText { get; set; }

    /// <summary>
    /// Dice rolls requested with the post (doc 4.2.2.13). The server rolls
    /// them and persists the results; only honored when the room allows dice.
    /// </summary>
    public IEnumerable<CreatePostDiceRoll> DiceRolls { get; set; } = [];
}

/// <summary>
/// A single dice roll requested with a new post. The results are generated
/// server-side by <see cref="IDiceRoller"/>.
/// </summary>
public class CreatePostDiceRoll
{
    /// <summary>
    /// Number of single die edges/sides (Y in XdY, e.g. 20 for a d20)
    /// </summary>
    public int EdgesCount { get; set; }

    /// <summary>
    /// Number of dice to roll (X in XdY)
    /// </summary>
    public int DiceCount { get; set; } = 1;

    /// <summary>
    /// Constant bonus added to the total
    /// </summary>
    public int Bonus { get; set; }

    /// <summary>
    /// Maximum explosions per die when it rolls its maximum value.
    /// null or 0 means the dice do not explode.
    /// </summary>
    public int? ExplosionCount { get; set; }

    /// <summary>
    /// Only the GM and the post author can see hidden rolls (the inverse of
    /// the composer's "показывать всем результаты" checkbox).
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Roll comment
    /// </summary>
    public string? Comment { get; set; }
}
