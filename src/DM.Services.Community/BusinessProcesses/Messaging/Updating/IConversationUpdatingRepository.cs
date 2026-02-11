using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.DataAccess.RelationalStorage;
using DbConversation = DM.Services.DataAccess.BusinessObjects.Messaging.Conversation;
using DbConversationLink = DM.Services.DataAccess.BusinessObjects.Messaging.UserConversationLink;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <summary>
/// Repository for updating conversations
/// </summary>
internal interface IConversationUpdatingRepository
{
    /// <summary>
    /// Get conversation for update validation
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <returns>Conversation with participants</returns>
    Task<Conversation?> Get(Guid conversationId);

    /// <summary>
    /// Update conversation
    /// </summary>
    /// <param name="update">Update builder</param>
    /// <param name="addLinks">Links to add</param>
    /// <param name="removeUserIds">User IDs to remove from conversation</param>
    /// <returns>Updated conversation</returns>
    Task<Conversation> Update(
        IUpdateBuilder<DbConversation> update,
        IEnumerable<DbConversationLink> addLinks,
        IEnumerable<Guid> removeUserIds);
}
