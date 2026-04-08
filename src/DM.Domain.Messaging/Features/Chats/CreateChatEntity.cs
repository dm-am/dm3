using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// DTO for creating a chat entity in repository
/// </summary>
public class CreateChatEntity
{
    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// Chat type (Direct or Group)
    /// </summary>
    public ChatType Type { get; set; }

    /// <summary>
    /// Chat title (for group chats)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Room identifier (for game room chats)
    /// </summary>
    public Guid? RoomId { get; set; }
}

/// <summary>
/// DTO for creating a chat link entity (user-chat relationship)
/// </summary>
public class CreateChatLinkEntity
{
    /// <summary>
    /// Link identifier
    /// </summary>
    public Guid UserChatLinkId { get; set; }

    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Whether the link is removed
    /// </summary>
    public bool IsRemoved { get; set; }
}
