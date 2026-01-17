using System;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Chat.Reading;

namespace DM.Services.Community.BusinessProcesses.Chat.Likes;

/// <summary>
/// Service for liking chat messages
/// </summary>
public interface IChatLikesService
{
    /// <summary>
    /// Like a chat message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <returns>Updated message</returns>
    Task<ChatMessage> Like(Guid id);

    /// <summary>
    /// Unlike a chat message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <returns>Updated message</returns>
    Task<ChatMessage> Unlike(Guid id);
}
