using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Features.Games;

public class GameDetails : Game
{
    public IEnumerable<Room> Rooms { get; set; } = [];

    public IEnumerable<UserReference> Subscribers { get; set; } = [];

    public IEnumerable<GeneralUser> FullAssistants { get; set; } = [];

    public IEnumerable<CharacterShortInfo> Characters { get; set; } = [];

    public string Info { get; set; } = null!;

    /// <summary>
    /// Only the leads and the post author see a roll result
    /// </summary>
    public bool HideDiceResult { get; set; }

    /// <summary>
    /// Any user can read private messages within posts
    /// </summary>
    public bool ShowPrivateMessages { get; set; }

    /// <summary>
    /// Hide posts count and last post date from everyone but the leads
    /// </summary>
    public bool HidePostStats { get; set; }

    /// <summary>
    /// Every game has one, so a details read that cannot find it fails
    /// </summary>
    public GameAttributeSchema AttributeSchema { get; set; } = null!;

    /// <summary>
    /// Posts across all rooms
    /// </summary>
    public int TotalPostsCount { get; set; }

    public int MasterPostsCount { get; set; }

    public DateTimeOffset? LastMasterPostUtc { get; set; }

    /// <summary>
    /// Some room of the game has dice enabled
    /// </summary>
    public bool DiceSupported { get; set; }
}
