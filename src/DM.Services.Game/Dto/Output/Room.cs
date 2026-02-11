using System;
using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Game.Dto.Output;

/// <summary>
/// DTO Model for game room
/// </summary>
public class Room
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Room content type
    /// </summary>
    public RoomType Type { get; set; }

    /// <summary>
    /// Room access type (visibility)
    /// </summary>
    public RoomAccessType AccessType { get; set; }

    /// <summary>
    /// Room access links (characters and readers)
    /// </summary>
    public IEnumerable<RoomAccess> Accesses { get; set; } = [];

    /// <summary>
    /// Post pendencies
    /// </summary>
    public IEnumerable<PostPendency> Pendencies { get; set; } = [];

    /// <summary>
    /// Total room posts count
    /// </summary>
    public int TotalPostsCount { get; set; }

    /// <summary>
    /// Unread posts count
    /// </summary>
    public int UnreadPostsCount { get; set; }

    /// <summary>
    /// Room order number
    /// </summary>
    public double OrderNumber { get; set; }

    /// <summary>
    /// Previous room identifier
    /// </summary>
    public Guid? PreviousRoomId { get; set; }

    /// <summary>
    /// Room settings
    /// </summary>
    public RoomSettings Settings { get; set; } = null!;

    /// <summary>
    /// Default room name
    /// </summary>
    public const string DefaultRoomName = "Основная комната";
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
