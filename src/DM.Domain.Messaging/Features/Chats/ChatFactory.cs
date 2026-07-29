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

    /// <inheritdoc />
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
                "Cannot create a direct chat with yourself");
        }

        var chatId = _guidFactory.Create();
        var chat = new CreateChatEntity
        {
            ChatId = chatId,
            Type = ChatType.Direct
        };
        var links = new[] { userId, otherUserId }
            .Select(id => new CreateChatLinkEntity
            {
                UserChatLinkId = _guidFactory.Create(),
                ChatId = chatId,
                UserId = id,
                IsRemoved = false
            });

        return (chat, links);
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
        var links = participantIds
            .Distinct()
            .Select(id => new CreateChatLinkEntity
            {
                UserChatLinkId = _guidFactory.Create(),
                ChatId = chatId,
                UserId = id,
                IsRemoved = false
            });

        return (chat, links);
    }

    /// <inheritdoc />
    public CreateChatEntity CreateGameRoom(Guid roomId, string title)
    {
        return new CreateChatEntity
        {
            ChatId = _guidFactory.Create(),
            Type = ChatType.GameRoom,
            Title = title,
            RoomId = roomId
        };
    }
}
