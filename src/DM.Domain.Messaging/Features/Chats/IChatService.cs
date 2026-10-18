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
    /// Count unread messages of the current user across every conversation
    /// </summary>
    /// <remarks>
    /// The badge in the header used to add up the per-chat counters of the first
    /// page of the chat list, so a reader with unread messages further down the
    /// list was shown a number that left them out, and the badge cost a page of
    /// conversations with their participants and last messages to compute.
    /// A conversation's marker is parented by its reader, so the whole total is
    /// one parent-scoped aggregate — see IUnreadCountersRepository on what
    /// ParentId holds.
    /// </remarks>
    /// <returns>Total number of unread messages</returns>
    Task<long> CountUnreadAsync();

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

    /// <summary>
    /// Leave a group chat
    /// </summary>
    /// <remarks>
    /// The chat goes out of the leaver's list and reads, and with it his unread
    /// marker. The others keep the chat and every message in it, the leaver's
    /// included. A direct chat is not left: the conversation of two has nobody
    /// to stay in it.
    /// </remarks>
    /// <param name="chatId">Chat identifier</param>
    Task LeaveAsync(Guid chatId);

    /// <summary>
    /// Clear the history of a direct or group chat for the current user
    /// </summary>
    /// <remarks>
    /// Nothing is deleted: the reader's own link to the chat remembers the moment,
    /// and the messages written up to it are no longer given to him. The other
    /// participants keep them. What was unread for him is read now, there is
    /// nothing left to read it in.
    /// </remarks>
    /// <param name="chatId">Chat identifier</param>
    Task ClearHistoryAsync(Guid chatId);

    /// <summary>
    /// Whether the current user may leave the chat: true exactly when
    /// <see cref="LeaveAsync" /> takes it
    /// </summary>
    /// <param name="chat">The chat, read for the current user</param>
    bool MayLeave(Chat chat);

    /// <summary>
    /// Whether the current user may clear the history of the chat for himself:
    /// true exactly when <see cref="ClearHistoryAsync" /> takes it
    /// </summary>
    /// <param name="chat">The chat, read for the current user</param>
    bool MayClearHistory(Chat chat);

    // ═══ DELETE ═══

    /// <summary>
    /// Delete a chat
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    Task DeleteAsync(Guid chatId);
}
