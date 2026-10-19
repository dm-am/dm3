using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Rooms;

public class CreateRoom
{
    public Guid GameId { get; set; }

    public string Title { get; set; } = null!;

    public RoomType Type { get; set; }

    public RoomAccessType AccessType { get; set; }

    /// <summary>
    /// Any user can read private messages within posts
    /// </summary>
    public bool ViewPrivateText { get; set; }

    /// <summary>
    /// Any user can see dice roll results
    /// </summary>
    public bool ViewDiceResults { get; set; } = true;

    public bool DiceEnabled { get; set; } = true;
}
