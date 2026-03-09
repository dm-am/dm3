using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// Unified repository for chat operations
/// </summary>
public interface IChatRepository
{
    // ═══ READ ═══

    /// <summary>
    /// Count user participated chats
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<int> Count(Guid userId);

    /// <summary>
    /// Get list of user participated chats
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="paging">Paging data</param>
    /// <returns></returns>
    Task<IEnumerable<Chat>> Get(Guid userId, PagingData paging);

    /// <summary>
    /// Get single chat for user
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<Chat?> Get(Guid chatId, Guid userId);

    /// <summary>
    /// Get chat for update validation (without user filter)
    /// </summary>
    /// <param name="chatId">Chat ID</param>
    /// <returns>Chat with participants</returns>
    Task<Chat?> GetForUpdate(Guid chatId);

    /// <summary>
    /// Find user for chat by username
    /// </summary>
    /// <param name="username">Username</param>
    /// <returns></returns>
    Task<Guid?> FindUser(string username);

    /// <summary>
    /// Find existing direct (1-on-1) chat
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="otherUserId">Other user identifier</param>
    /// <returns></returns>
    Task<Chat?> FindDirectChat(Guid userId, Guid otherUserId);

    // ═══ WRITE ═══

    /// <summary>
    /// Save chat
    /// </summary>
    /// <param name="chat">Chat data</param>
    /// <param name="chatLinks">Chat links data</param>
    /// <returns></returns>
    Task<Chat> Create(CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> chatLinks);

    /// <summary>
    /// Update chat
    /// </summary>
    /// <param name="update">Update data including links to add/remove</param>
    /// <returns>Updated chat</returns>
    Task<Chat> Update(UpdateChatEntity update);
}
