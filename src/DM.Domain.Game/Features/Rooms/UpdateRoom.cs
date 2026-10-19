using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Rooms;

public class UpdateRoom
{
    public Guid RoomId { get; set; }

    public string? Title { get; set; }

    public RoomType? Type { get; set; }

    public RoomAccessType? AccessType { get; set; }

    public Optional<Guid>? PreviousRoomId { get; set; }

    /// <summary>
    /// Any user can read private messages within posts
    /// </summary>
    public bool? ViewPrivateText { get; set; }

    /// <summary>
    /// Any user can see dice roll results
    /// </summary>
    public bool? ViewDiceResults { get; set; }

    public bool? DiceEnabled { get; set; }

    public bool? HiddenWithoutAccess { get; set; }

    public bool? IsArchived { get; set; }

    /// <summary>
    /// For RoomType.Chat
    /// </summary>
    public Guid? ChatId { get; set; }
}
