using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Rooms;

public class CreateRoomEntity
{
    public Guid RoomId { get; set; }

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
    public bool ViewDiceResults { get; set; }

    public bool DiceEnabled { get; set; }

    public bool IsArchived { get; set; }
}

/// <summary>
/// A null field is left unchanged
/// </summary>
public class UpdateRoomEntity
{
    public Guid RoomId { get; set; }

    public string? Title { get; set; }

    public RoomType? Type { get; set; }

    public RoomAccessType? AccessType { get; set; }

    /// <summary>
    /// Room of the same game this one is put right after; null puts it first.
    /// Read only when <see cref="ShouldReorder"/> is set.
    /// </summary>
    public Guid? NewPreviousRoomId { get; set; }

    /// <summary>
    /// Whether the room moves to the place <see cref="NewPreviousRoomId"/> names
    /// </summary>
    public bool ShouldReorder { get; set; }

    public bool? ViewPrivateText { get; set; }

    public bool? ViewDiceResults { get; set; }

    public bool? DiceEnabled { get; set; }

    public bool? HiddenWithoutAccess { get; set; }

    public bool? IsArchived { get; set; }

    /// <summary>
    /// Written when <see cref="ShouldSetChatId"/> is set
    /// </summary>
    public Guid? ChatId { get; set; }

    public bool ShouldSetChatId { get; set; }
}
