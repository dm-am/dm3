using System;
using System.Collections.Generic;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// DTO for updating a chat entity in repository
/// </summary>
public class UpdateChatEntity
{
    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// Updated title (null to keep current)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Updated last message ID (null to keep current)
    /// </summary>
    public Guid? LastMessageId { get; set; }

    /// <summary>
    /// Chat links to add
    /// </summary>
    public IEnumerable<CreateChatLinkEntity> AddLinks { get; set; } = [];

    /// <summary>
    /// User IDs to remove from chat
    /// </summary>
    public IEnumerable<Guid> RemoveUserIds { get; set; } = [];
}
