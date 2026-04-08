using System;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Game.ChatRooms;

/// <summary>
/// Request model for updating a chat room
/// </summary>
public class UpdateChatRoom
{
    /// <summary>
    /// Updated chat room title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Updated previous room identifier (for reordering)
    /// </summary>
    public Optional<Guid>? PreviousRoomId { get; set; }
}
