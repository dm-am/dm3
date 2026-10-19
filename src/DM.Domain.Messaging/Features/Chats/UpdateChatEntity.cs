using System;
using System.Collections.Generic;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// DTO for updating a chat entity in repository: a field left null keeps the current value
/// </summary>
public class UpdateChatEntity
{
    public Guid ChatId { get; set; }

    public string? Title { get; set; }

    public IEnumerable<CreateChatLinkEntity> AddLinks { get; set; } = [];

    public IEnumerable<Guid> RemoveUserIds { get; set; } = [];
}
