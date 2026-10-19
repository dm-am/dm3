using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.PostPendencies;
using DM.Domain.Game.Features.RoomAccesses;

namespace DM.Domain.Game.Features.Rooms;

public class RoomSettings
{
    /// <summary>
    /// Any user can read private messages within posts
    /// </summary>
    public bool ViewPrivateText { get; set; }

    /// <summary>
    /// Any user can see dice roll results
    /// </summary>
    public bool ViewDiceResults { get; set; }

    public bool DiceEnabled { get; set; }

    /// <summary>
    /// Kept out of the room list for everybody who may not open it; off by default
    /// </summary>
    public bool HiddenWithoutAccess { get; set; }
}

public class Room
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable number within the game, for the URL
    /// </summary>
    public int RoomNumber { get; set; }

    public Guid GameId { get; set; }

    /// <summary>
    /// For RoomType.Chat
    /// </summary>
    public Guid? ChatId { get; set; }

    public string Title { get; set; } = null!;

    public RoomType Type { get; set; }

    public RoomAccessType AccessType { get; set; }

    /// <summary>
    /// Hidden from the active rooms list, kept for history
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// False only in the rooms listing, which names closed rooms the reader may not enter
    /// </summary>
    public bool CanView { get; set; } = true;

    public IEnumerable<RoomAccess> Accesses { get; set; } = [];

    public IEnumerable<PostPendency> Pendencies { get; set; } = [];

    /// <summary>
    /// Posts, or chat messages of a chat room, removed ones left out
    /// </summary>
    public int TotalPostsCount { get; set; }

    public int UnreadPostsCount { get; set; }

    public double OrderNumber { get; set; }

    /// <summary>
    /// For the menu line of a room the reader may not open; empty where the accesses say it
    /// </summary>
    public IEnumerable<string> CharacterNames { get; set; } = [];

    public RoomSettings Settings { get; set; } = null!;
}

/// <summary>
/// Carries the whole game, so a tooltip needs no second request per post.
/// </summary>
public class RoomRef
{
    public Guid Id { get; set; }

    public int RoomNumber { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>
    /// Filled after paging by a batched read; null when the game has been removed
    /// </summary>
    public Games.Game? Game { get; set; }
}

public class RoomToUpdate : Room
{
    /// <summary>
    /// For authorization
    /// </summary>
    public Games.Game Game { get; set; } = null!;

    /// <summary>
    /// The characters a hidden block or a file of a post here may be addressed to, with their owners.
    /// </summary>
    /// <remarks>
    /// Those let into the room, so an addressee is never handed what the room keeps from him.
    /// </remarks>
    public IEnumerable<PrivateAddressee> PrivateAddressees() =>
        Accesses
            .Select(a => a.Character)
            .Where(c => c is { Author: not null })
            .Select(c => new PrivateAddressee(c!.Id, c.Name, c.Author!.UserId));
}
