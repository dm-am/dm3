using System;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// DTO for creating a link between a user and a chat
/// </summary>
public class CreateChatLinkEntity
{
    public Guid UserChatLinkId { get; set; }

    public Guid ChatId { get; set; }

    public Guid UserId { get; set; }

    public bool IsRemoved { get; set; }
}
