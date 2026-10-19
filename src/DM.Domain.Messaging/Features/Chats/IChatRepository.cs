using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Chats;

public interface IChatRepository
{
    Task<int> Count(Guid userId);

    Task<IEnumerable<Chat>> Get(Guid userId, PagingData paging);

    /// <summary>
    /// The chat if the user takes part in it, null otherwise
    /// </summary>
    Task<Chat?> Get(Guid chatId, Guid userId);

    /// <summary>
    /// The chat by its public identifier (5 letters) if the user takes part in it
    /// </summary>
    Task<Chat?> GetByPublicId(string publicId, Guid userId);

    /// <summary>
    /// The chat with its participants, without the participation filter
    /// </summary>
    Task<Chat?> GetForUpdate(Guid chatId);

    Task<Chat?> FindDirectChat(Guid userId, Guid otherUserId);

    Task<Chat> Create(CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> chatLinks);

    /// <summary>
    /// Apply the update, links to add and remove included, and read the chat back for
    /// <paramref name="viewerId"/>
    /// </summary>
    Task<Chat> Update(UpdateChatEntity update, Guid viewerId);

    /// <summary>
    /// Take the participant out of the chat, by his own will
    /// </summary>
    Task Leave(Guid chatId, Guid userId, DateTimeOffset leftUtc);

    /// <summary>
    /// Mark the history of the chat cleared for one participant: messages created
    /// at or before the moment are no longer given to him
    /// </summary>
    Task ClearHistory(Guid chatId, Guid userId, DateTimeOffset clearedUtc);

    /// <summary>
    /// Create a game room chat, which has no user links
    /// </summary>
    Task<Chat> CreateGameRoomChat(CreateChatEntity chat);

    Task Delete(Guid chatId);
}
