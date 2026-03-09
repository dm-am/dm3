using System;

namespace DM.Domain.Messaging.Features.Messages;

/// <summary>
/// Factory for message data
/// </summary>
internal interface IMessageFactory
{
    /// <summary>
    /// Create new message data
    /// </summary>
    /// <param name="createMessage"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    CreateMessageEntity Create(CreateMessage createMessage, Guid userId);
}
