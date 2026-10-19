using System;

namespace DM.Domain.Messaging.Features.Messages;

public class UpdateChatLastMessageEntity
{
    public Guid ChatId { get; set; }

    public Guid LastMessageId { get; set; }
}
