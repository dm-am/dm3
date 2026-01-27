using System;
using DM.Services.DataAccess.BusinessObjects.Messaging;

namespace DM.Services.Community.BusinessProcesses.Chat.Creating;

/// <summary>
/// Factory for chat message DAL model
/// </summary>
internal interface IChatMessageFactory
{
    /// <summary>
    /// Create new DAL model for global chat
    /// </summary>
    /// <param name="createChatMessage">Creating DTO model</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Message Create(CreateChatMessage createChatMessage, Guid userId);
}
