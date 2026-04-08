using System;
using System.Collections.Generic;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Rooms;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// DTO model for game post
/// </summary>
public class Post
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Parent room
    /// </summary>
    public Room Room { get; set; } = null!;

    /// <summary>
    /// Post character
    /// </summary>
    public Character Character { get; set; } = null!;

    /// <summary>
    /// Post author (lightweight reference)
    /// </summary>
    public UserRef Author { get; set; } = null!;

    /// <summary>
    /// Author's game role: DungeonMaster, Assistant, or null for player posts
    /// </summary>
    public string? AuthorGameRole { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Edit history (most recent first)
    /// </summary>
    public IEnumerable<PostEditInfo>? Edits { get; set; }

    /// <summary>
    /// Game text (in-character content)
    /// </summary>
    public PostBbText GameText { get; set; } = null!;

    /// <summary>
    /// Metagame text (OOC commentary)
    /// </summary>
    public CommonBbText MetagameText { get; set; } = null!;

    /// <summary>
    /// Dice roll results
    /// </summary>
    public IEnumerable<DiceRoll> DiceRolls { get; set; } = [];

    /// <summary>
    /// Sum of review scores
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Number of reviews
    /// </summary>
    public int ReviewCount { get; set; }
}

/// <summary>
/// DTO model for post edit history entry
/// </summary>
public class PostEditInfo
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Edit timestamp (UTC)
    /// </summary>
    public DateTimeOffset EditedUtc { get; set; }

    /// <summary>
    /// Editor user (lightweight reference)
    /// </summary>
    public UserRef Editor { get; set; } = null!;
}
