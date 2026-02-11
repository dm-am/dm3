using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DbConversation = DM.Services.DataAccess.BusinessObjects.Messaging.Conversation;
using DbConversationLink = DM.Services.DataAccess.BusinessObjects.Messaging.UserConversationLink;

namespace DM.Services.Community.BusinessProcesses.Messaging.Reading;

/// <summary>
/// Storage for reading user conversations
/// </summary>
internal interface IConversationReadingRepository
{
    /// <summary>
    /// Count user participated conversations
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<int> Count(Guid userId);

    /// <summary>
    /// Get list of user participated conversations
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="paging">Paging data</param>
    /// <returns></returns>
    Task<IEnumerable<Conversation>> Get(Guid userId, PagingData paging);

    /// <summary>
    /// Get single conversation for user
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<Conversation?> Get(Guid conversationId, Guid userId);

    /// <summary>
    /// Find user for conversation by login
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns></returns>
    Task<Guid?> FindUser(string login);

    /// <summary>
    /// Find existing direct (1-on-1) conversation
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="otherUserId">Other user identifier</param>
    /// <returns></returns>
    Task<Conversation?> FindDirectConversation(Guid userId, Guid otherUserId);

    /// <summary>
    /// Save conversation
    /// </summary>
    /// <param name="conversation"></param>
    /// <param name="conversationLinks"></param>
    /// <returns></returns>
    Task<Conversation> Create(DbConversation conversation, IEnumerable<DbConversationLink> conversationLinks);
}