using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// API DTO for editing an existing room
/// </summary>
/// <remarks>
/// The endpoint used to take the Room response DTO, so the contract asked for
/// the room number, the game back-reference, the access list, the pendency list
/// and an unread counter in order to rename a room — all server-owned, all held
/// off by Ignore() in the mapping profile rather than by the shape of the
/// request.
///
/// Every field is optional and an omitted one leaves the stored value alone.
/// PreviousRoomId is three-valued on purpose: absent means "do not reorder",
/// an explicit null means "move to the head of the chain".
/// </remarks>
public class UpdateRoomRequest
{
    /// <summary>
    /// Room title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Room content type
    /// </summary>
    public RoomType? Type { get; set; }

    /// <summary>
    /// Room access type
    /// </summary>
    public RoomAccessType? Access { get; set; }

    /// <summary>
    /// Room is archived (hidden from the active rooms list, kept for history)
    /// </summary>
    public bool? IsArchived { get; set; }

    /// <summary>
    /// Room this one follows. Absent leaves the order alone, null moves the
    /// room to the head of the chain.
    /// </summary>
    public Optional<Guid>? PreviousRoomId { get; set; }

    /// <summary>
    /// Room settings. An omitted block leaves every setting unchanged.
    /// </summary>
    public RoomSettings? Settings { get; set; }
}

/// <summary>
/// API DTO for editing a room access grant
/// </summary>
/// <remarks>
/// Only the policy is editable. Which character or reader the grant belongs to
/// is fixed at creation; it used to travel in the PATCH body as a full Character
/// and a full User, neither of which the update path reads.
/// </remarks>
public class UpdateRoomAccessRequest
{
    /// <summary>
    /// Access policy
    /// </summary>
    public RoomAccessPolicy Policy { get; set; }
}
