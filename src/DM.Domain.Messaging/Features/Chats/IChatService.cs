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
    /// <remarks>
    /// Refuses the whole chat when one of the invited participants keeps the
    /// author on a personal blacklist with private messages blocked: a group is
    /// not a way around that setting.
    /// </remarks>
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
    /// Get a game room chat, without the participation check
    /// </summary>
    /// <remarks>
    /// A game room chat is created with no participant rows at all, so the
    /// participation check that guards every other chat refuses it to everyone,
    /// the master of the game included. Who may read and write there is access to
    /// the room, decided by the game module before this call. The method accepts
    /// nothing but a game room chat, so it cannot become a way around the
    /// participation check for direct and group chats.
    /// </remarks>
    /// <param name="chatId">Chat identifier</param>
    /// <returns>Chat</returns>
    Task<Chat> GetGameRoomAsync(Guid chatId);

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
    /// <remarks>
    /// Adding a participant answers to the same rule as creating the chat does:
    /// somebody who keeps the caller on a personal blacklist with private
    /// messages blocked is not added.
    /// </remarks>
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
