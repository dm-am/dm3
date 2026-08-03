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
    /// Room title. Always set on read; nullable so an omitted value on a
    /// partial PATCH leaves the stored title unchanged (UpdateRoom.Title is
    /// nullable) instead of failing implicit-required model validation.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Room access type
    /// </summary>
    public RoomAccessType? Access { get; set; }

    /// <summary>
    /// Room content type. Nullable so an omitted value on a partial PATCH
    /// leaves the stored RoomType unchanged instead of resetting it to Default.
    /// </summary>
    public RoomType? Type { get; set; }

    /// <summary>
    /// Room is archived (hidden from the active rooms list, kept for history).
    /// Nullable on input so an omitted value on a partial PATCH is unchanged.
    /// </summary>
    public bool? IsArchived { get; set; }

    /// <summary>
    /// Reader may open the room. The rooms listing names every room of the
    /// game and answers false for a private one the reader may not enter; a
    /// read that returns a room at all returns one they may.
    /// </summary>
    public bool CanView { get; set; } = true;

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
    /// Room settings. Always set on read; nullable so an omitted block on a
    /// partial PATCH leaves every setting unchanged (the mapping already
    /// treats a null Settings as "no changes").
    /// </summary>
    public RoomSettings? Settings { get; set; }
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
