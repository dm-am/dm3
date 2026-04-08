using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// Unified service for chat operations
/// </summary>
public interface IChatService
{
    // ═══ CREATE ═══

    /// <summary>
    /// Create a new group chat
    /// </summary>
    /// <param name="createChat">Chat data</param>
    /// <returns>Created chat</returns>
    Task<Chat> CreateGroupAsync(CreateChat createChat);

    /// <summary>
    /// Create a new game room chat
    /// </summary>
    /// <param name="roomId">Room identifier to link</param>
    /// <param name="title">Chat title</param>
    /// <returns>Created chat</returns>
    Task<Chat> CreateGameRoomChatAsync(Guid roomId, string title);

    // ═══ READ ═══

    /// <summary>
    /// Get list of user chats
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <returns>Chats and paging result</returns>
    Task<(IEnumerable<Chat> Chats, PagingResult Paging)> GetAsync(PagingQuery query);

    /// <summary>
    /// Get single chat
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    /// <returns>Chat</returns>
    Task<Chat> GetAsync(Guid chatId);

    /// <summary>
    /// Get single chat by public ID
    /// </summary>
    /// <param name="publicId">Chat public ID (5 letters)</param>
    /// <returns>Chat</returns>
    Task<Chat> GetByPublicIdAsync(string publicId);

    /// <summary>
    /// Find or create direct chat by username
    /// </summary>
    /// <param name="username">Username</param>
    /// <returns>Direct chat</returns>
    Task<Chat> GetOrCreateDirectAsync(string username);

    /// <summary>
    /// Count all unread chats
    /// </summary>
    /// <returns>Unread count</returns>
    Task<int> GetTotalUnreadCountAsync();

    /// <summary>
    /// Mark all chat messages as read
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    Task MarkAsReadAsync(Guid chatId);

    // ═══ UPDATE ═══

    /// <summary>
    /// Update chat (title and/or participants)
    /// </summary>
    /// <param name="updateChat">Update data</param>
    /// <returns>Updated chat</returns>
    Task<Chat> UpdateAsync(UpdateChat updateChat);

    // ═══ DELETE ═══

    /// <summary>
    /// Delete a chat
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    Task DeleteAsync(Guid chatId);
}
