using System;
using System.Collections.Generic;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// Factory for chat data
/// </summary>
internal interface IChatFactory
{
    /// <summary>
    /// Create data for direct (1-on-1) chat
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="otherUserId">Other user identifier</param>
    /// <returns></returns>
    (CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> links) CreateDirect(Guid userId, Guid otherUserId);

    /// <summary>
    /// Create data for group chat
    /// </summary>
    /// <param name="title">Chat title</param>
    /// <param name="participantIds">Participant user identifiers (including creator)</param>
    /// <returns></returns>
    (CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> links) CreateGroup(string title, IEnumerable<Guid> participantIds);
}
