using System;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Chat.Reading;

namespace DM.Services.Community.BusinessProcesses.Chat.Updating;

/// <summary>
/// Service for updating chat messages
/// </summary>
public interface IChatUpdatingService
{
    /// <summary>
    /// Update chat message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <param name="text">New message text</param>
    /// <returns>Updated message</returns>
    Task<ChatMessage> Update(Guid id, string text);
}
