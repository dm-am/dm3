using System;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Chat.Reading;

namespace DM.Services.Community.BusinessProcesses.Chat.Updating;

/// <summary>
/// Repository for updating chat messages
/// </summary>
internal interface IChatUpdatingRepository
{
    /// <summary>
    /// Update chat message text
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <param name="text">New message text</param>
    /// <returns>Updated message</returns>
    Task<ChatMessage> Update(Guid id, string text);
}
