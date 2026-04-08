using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Game.Games;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// DTO model for game room
/// </summary>
public class Room
{
    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room number within game (for URL, stable)
    /// </summary>
    public int RoomNumber { get; set; }

    /// <summary>
    /// Game reference (for navigation)
    /// </summary>
    public GameRef? Game { get; set; }

    /// <summary>
    /// Previous room identifier
    /// </summary>
    public Optional<Guid>? PreviousRoomId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Room access type
    /// </summary>
    public RoomAccessType? Access { get; set; }

    /// <summary>
    /// Room content type
    /// </summary>
    public RoomType? Type { get; set; }

    /// <summary>
    /// Room accesses
    /// </summary>
    public IEnumerable<RoomAccess> Accesses { get; set; } = [];

    /// <summary>
    /// Post pendencies
    /// </summary>
    public IEnumerable<PostPendency> Pendencies { get; set; } = [];

    /// <summary>
    /// Number of unread posts
    /// </summary>
    public int UnreadPostsCount { get; set; }

    /// <summary>
    /// Room settings
    /// </summary>
    public RoomSettings Settings { get; set; } = null!;
}

/// <summary>
/// DTO model for room settings
/// </summary>
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

    /// <summary>
    /// Dice rolling is enabled in this room
    /// </summary>
    public bool DiceEnabled { get; set; }
}
