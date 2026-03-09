using System;
using System.Collections.Generic;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Features.Community.Users;
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
    /// Post author
    /// </summary>
    public User Author { get; set; } = null!;

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last update moment
    /// </summary>
    public DateTimeOffset? UpdatedUtc { get; set; }

    /// <summary>
    /// Text
    /// </summary>
    public PostBbText Text { get; set; } = null!;

    /// <summary>
    /// Additional text
    /// </summary>
    public CommonBbText Commentary { get; set; } = null!;

    /// <summary>
    /// Private text to master
    /// </summary>
    public CommonBbText MasterMessage { get; set; } = null!;

    /// <summary>
    /// Dice roll results
    /// </summary>
    public IEnumerable<DiceRoll> DiceRolls { get; set; } = [];
}
