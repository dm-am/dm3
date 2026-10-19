using System.Collections.Generic;
using System.Linq;
using System.Net;
using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Messaging.Features.Chats;

/// <inheritdoc />
internal class ChatFactory : IChatFactory
{
    private readonly IGuidFactory _guidFactory;

    public ChatFactory(
        IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public (CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> links) CreateDirect(Guid userId, Guid otherUserId)
    {
        if (userId == otherUserId)
        {
            throw new HttpException(HttpStatusCode.BadRequest,
                "Нельзя начать чат с самим собой");
        }

        var chatId = _guidFactory.Create();
        var chat = new CreateChatEntity
        {
            ChatId = chatId,
            Type = ChatType.Direct
        };
        return (chat, CreateLinks(chatId, [userId, otherUserId]));
    }

    /// <inheritdoc />
    public (CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> links) CreateGroup(string title, IEnumerable<Guid> participantIds)
    {
        var chatId = _guidFactory.Create();
        var chat = new CreateChatEntity
        {
            ChatId = chatId,
            Type = ChatType.Group,
            Title = title
        };
        return (chat, CreateLinks(chatId, participantIds));
    }

    /// <inheritdoc />
    public CreateChatEntity CreateGameRoom(Guid roomId, string title) => new()
    {
        ChatId = _guidFactory.Create(),
        Type = ChatType.GameRoom,
        Title = title,
        RoomId = roomId
    };

    /// <inheritdoc />
    public IEnumerable<CreateChatLinkEntity> CreateLinks(Guid chatId, IEnumerable<Guid> userIds) =>
        userIds.Select(id => new CreateChatLinkEntity
        {
            UserChatLinkId = _guidFactory.Create(),
            ChatId = chatId,
            UserId = id
        });
}
