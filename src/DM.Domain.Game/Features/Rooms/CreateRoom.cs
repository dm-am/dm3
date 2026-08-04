using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Rooms;

/// <summary>
/// DTO for room creating
/// </summary>
public class CreateRoom
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Room type
    /// </summary>
    public RoomType Type { get; set; }

    /// <summary>
    /// Room access type
    /// </summary>
    public RoomAccessType AccessType { get; set; }

    /// <summary>
    /// Any user can read private messages within posts
    /// </summary>
    public bool ViewPrivateText { get; set; }

    /// <summary>
    /// Any user can see dice roll results
    /// </summary>
    public bool ViewDiceResults { get; set; } = true;

    /// <summary>
    /// Dice rolling is enabled in this room
    /// </summary>
    public bool DiceEnabled { get; set; } = true;
}
