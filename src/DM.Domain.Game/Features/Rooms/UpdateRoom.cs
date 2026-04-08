using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Rooms;

/// <summary>
/// DTO for room updating
/// </summary>
public class UpdateRoom
{
    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Room type
    /// </summary>
    public RoomType? Type { get; set; }

    /// <summary>
    /// Room access type
    /// </summary>
    public RoomAccessType? AccessType { get; set; }

    /// <summary>
    /// Previous room identifier
    /// </summary>
    public Optional<Guid>? PreviousRoomId { get; set; }

    /// <summary>
    /// Any user can read private messages within posts
    /// </summary>
    public bool? ViewPrivateText { get; set; }

    /// <summary>
    /// Any user can see dice roll results
    /// </summary>
    public bool? ViewDiceResults { get; set; }

    /// <summary>
    /// Dice rolling is enabled in this room
    /// </summary>
    public bool? DiceEnabled { get; set; }

    #region Internal fields (set by service)

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool? IsRemoved { get; set; }

    /// <summary>
    /// Linked chat identifier (for RoomType.Chat rooms)
    /// </summary>
    public Guid? ChatId { get; set; }

    #endregion
}