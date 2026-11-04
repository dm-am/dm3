using System;

namespace DM.Domain.Messaging.Features.Messages;

internal interface IMessageFactory
{
    CreateMessageEntity Create(CreateMessage createMessage, Guid userId);
}
