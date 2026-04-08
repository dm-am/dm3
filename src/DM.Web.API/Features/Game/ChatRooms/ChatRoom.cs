using System;
using System.Collections.Generic;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Game.ChatRooms;

/// <summary>
/// Chat room in a game for player communication
/// </summary>
public class ChatRoom
{
    /// <summary>
    /// Chat room identifier (same as room identifier)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Linked chat identifier for messaging
    /// </summary>
    public Guid? ChatId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Chat room title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Display order number
    /// </summary>
    public double OrderNumber { get; set; }

    /// <summary>
    /// Number of unread messages
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// Access entries (who can read/write)
    /// </summary>
    public IEnumerable<ChatRoomAccess> Accesses { get; set; } = [];
}

/// <summary>
/// Chat room access entry
/// </summary>
public class ChatRoomAccess
{
    /// <summary>
    /// Access entry identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Target user (for Reader access)
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Target character (for Character access)
    /// </summary>
    public Character? Character { get; set; }
}
