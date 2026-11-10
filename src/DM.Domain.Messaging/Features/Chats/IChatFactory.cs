using System;
using System.Collections.Generic;

namespace DM.Domain.Messaging.Features.Chats;

internal interface IChatFactory
{
    (CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> links) CreateDirect(Guid userId, Guid otherUserId);

    /// <summary>
    /// Data of a group chat; the participants include the creator and come without repeats
    /// </summary>
    (CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> links) CreateGroup(string title, IEnumerable<Guid> participantIds);

    /// <summary>
    /// Data of a game room chat, which has no participant links
    /// </summary>
    CreateChatEntity CreateGameRoom(Guid roomId, string title);

    IEnumerable<CreateChatLinkEntity> CreateLinks(Guid chatId, IEnumerable<Guid> userIds);
}
