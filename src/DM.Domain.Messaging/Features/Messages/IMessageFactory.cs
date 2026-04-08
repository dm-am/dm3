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
    /// <param name="createMessage">Message creation data</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Message entity DTO</returns>
    CreateMessageEntity Create(CreateMessage createMessage, Guid userId);
}
