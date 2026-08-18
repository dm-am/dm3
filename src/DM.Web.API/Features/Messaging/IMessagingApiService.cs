using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using Chat = DM.Web.API.Features.Messaging.Chats.Chat;
using CreateChat = DM.Web.API.Features.Messaging.Chats.CreateChat;
using UpdateChat = DM.Web.API.Features.Messaging.Chats.UpdateChat;
using ChatAvailability = DM.Web.API.Features.Messaging.Chats.ChatAvailability;
using Message = DM.Web.API.Features.Messaging.Messages.Message;

namespace DM.Web.API.Features.Messaging;

/// <summary>
/// API service for messaging
/// </summary>
public interface IMessagingApiService
{
    /// <summary>
    /// Get list of user chats
    /// </summary>
    Task<(IEnumerable<Chat> Chats, PagingInfo Paging)> GetChatsAsync(PagingQuery query);

    /// <summary>
    /// Get or create direct chat with user by username
    /// </summary>
    Task<Chat> GetDirectChatAsync(string username);

    /// <summary>
    /// Get single chat by ID
    /// </summary>
    Task<Chat> GetChatAsync(Guid id);

    /// <summary>
    /// Get single chat by public ID (5 letters)
    /// </summary>
    Task<Chat> GetChatByPublicIdAsync(string publicId);

    /// <summary>
    /// Resolve a route identifier of a chat, given either form.
    /// Every chat-scoped route accepts both, so without a shared resolver
    /// each controller carries its own copy of the branch.
    /// </summary>
    /// <param name="idOrPublicId">Chat public id (5 letters) or GUID</param>
    Task<Guid> ResolveChatIdAsync(string idOrPublicId);

    /// <summary>
    /// Create a new group chat
    /// </summary>
    Task<Chat> CreateChatAsync(CreateChat createChat);

    /// <summary>
    /// Update an existing chat
    /// </summary>
    Task<Chat> UpdateChatAsync(Guid id, UpdateChat updateChat);

    /// <summary>
    /// Check if chat can be started with user
    /// </summary>
    Task<ChatAvailability> CanStartChatAsync(string username);

    /// <summary>
    /// Get list of chat messages with offset-based paging (legacy)
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    /// <param name="query">Paging query</param>
    /// <returns>List of messages with paging metadata</returns>
    Task<ListEnvelope<Message>> GetMessagesAsync(Guid chatId, PagingQuery query);

    /// <summary>
    /// Get list of chat messages with cursor-based pagination
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    /// <param name="cursor">Opaque cursor for pagination</param>
    /// <param name="aroundMessageId">Get messages around this message ID</param>
    /// <param name="nearTimestampUtc">Get messages near this UTC timestamp</param>
    /// <param name="limit">Maximum number of messages to return</param>
    /// <returns>List of messages with cursor-based pagination metadata</returns>
    Task<CursorEnvelope<Message>> GetMessagesWithCursorAsync(
        Guid chatId,
        string? cursor = null,
        Guid? aroundMessageId = null,
        DateTimeOffset? nearTimestampUtc = null,
        int limit = 50);

    /// <summary>
    /// Get list of game room chat messages with cursor-based pagination
    /// </summary>
    /// <remarks>
    /// For the game chat room endpoints, which authorize by the room. The chat
    /// endpoints must keep using the general reader above.
    /// </remarks>
    /// <param name="chatId">Chat identifier</param>
    /// <param name="cursor">Opaque cursor for pagination</param>
    /// <param name="limit">Maximum number of messages to return</param>
    /// <returns>List of messages with cursor-based pagination metadata</returns>
    Task<CursorEnvelope<Message>> GetGameRoomMessagesWithCursorAsync(
        Guid chatId,
        string? cursor = null,
        int limit = 50);

    /// <summary>
    /// Create new message
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    /// <param name="message">Message</param>
    /// <returns>Created message wrapped in envelope</returns>
    Task<Envelope<Message>> CreateMessageAsync(Guid chatId, Message message);

    /// <summary>
    /// Create new message in a game room chat
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    /// <param name="message">Message</param>
    /// <returns>Created message wrapped in envelope</returns>
    Task<Envelope<Message>> CreateGameRoomMessageAsync(Guid chatId, Message message);

    /// <summary>
    /// Get single message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <returns>Message details wrapped in envelope</returns>
    Task<Envelope<Message>> GetMessageAsync(Guid messageId);

    /// <summary>
    /// Update existing message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <param name="message">Message data</param>
    /// <returns>Updated message wrapped in envelope</returns>
    Task<Envelope<Message>> UpdateMessageAsync(Guid messageId, Message message);

    /// <summary>
    /// Delete single message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    Task DeleteMessageAsync(Guid messageId);

    /// <summary>
    /// Mark all chat messages as read
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    Task MarkAsReadAsync(Guid chatId);

    /// <summary>
    /// Like a message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <returns>Updated message with like information wrapped in envelope</returns>
    Task<Envelope<Message>> LikeMessageAsync(Guid messageId);

    /// <summary>
    /// Unlike a message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    Task UnlikeMessageAsync(Guid messageId);

    // ═══ GLOBAL CHAT ═══

    /// <summary>
    /// Get global chat messages with cursor-based pagination
    /// </summary>
    Task<CursorEnvelope<Message>> GetGlobalChatMessagesAsync(
        string? cursor = null,
        Guid? aroundMessageId = null,
        DateTimeOffset? nearTimestampUtc = null,
        int limit = 50);

    /// <summary>
    /// Create a message in global chat
    /// </summary>
    Task<Envelope<Message>> CreateGlobalChatMessageAsync(Message message);

    /// <summary>
    /// Mark global chat messages as read
    /// </summary>
    Task MarkGlobalChatAsReadAsync();
}
