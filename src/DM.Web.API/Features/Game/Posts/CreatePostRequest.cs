using System;
using System.Collections.Generic;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// Request model for creating a new post
/// </summary>
public class CreatePostRequest
{
    /// <summary>
    /// Character identifier (optional for master posts)
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
    /// them and returns the results; only honored when the room allows dice.
    /// </summary>
    public IEnumerable<CreatePostDiceRoll> DiceRolls { get; set; } = [];
}

/// <summary>
/// A single dice roll requested with a new post. The server generates the
/// results (per doc 4.2.2.13); the client only describes the roll.
/// </summary>
public class CreatePostDiceRoll
{
    /// <summary>
    /// Number of single die edges/sides (Y in XdY, e.g. 20 for a d20)
    /// </summary>
    public int Dice { get; set; }

    /// <summary>
    /// Number of dice to roll (X in XdY). Defaults to a single die.
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    /// Flat modifier added to the total
    /// </summary>
    public int Bonus { get; set; }

    /// <summary>
    /// Maximum explosions per die when it rolls its maximum value.
    /// null or 0 means the dice do not explode.
    /// </summary>
    public int? Explosion { get; set; }

    /// <summary>
    /// Whether the result is visible to everyone ("показывать всем
    /// результаты"). When false, only the GM and the author see it.
    /// </summary>
    public bool Public { get; set; } = true;

    /// <summary>
    /// Optional comment shown next to the roll
    /// </summary>
    public string? Comment { get; set; }
}
