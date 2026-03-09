using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using Chat = DM.Web.API.Features.Messaging.Chats.Chat;
using CreateChat = DM.Web.API.Features.Messaging.Chats.CreateChat;
using UpdateChat = DM.Web.API.Features.Messaging.Chats.UpdateChat;
using ChatAvailability = DM.Web.API.Features.Messaging.Chats.ChatAvailability;
using Conversation = DM.Web.API.Features.Messaging.Conversations.Conversation;
using CreateConversation = DM.Web.API.Features.Messaging.Conversations.CreateConversation;
using UpdateConversation = DM.Web.API.Features.Messaging.Conversations.UpdateConversation;
using Message = DM.Web.API.Features.Messaging.Messages.Message;

namespace DM.Web.API.Features.Messaging;

/// <summary>
/// API service for messaging
/// </summary>
public interface IMessagingApiService
{
    /// <summary>
    /// Get list of user conversations
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <returns></returns>
    Task<ListEnvelope<Conversation>> GetConversations(PagingQuery query);

    /// <summary>
    /// Get list of user chats (alias for GetConversations)
    /// </summary>
    Task<(IEnumerable<Chat> Chats, PagingInfo Paging)> GetChats(PagingQuery query);

    /// <summary>
    /// Get or create direct chat with user by username
    /// </summary>
    Task<Chat> GetDirectChat(string username);

    /// <summary>
    /// Get single chat by ID
    /// </summary>
    Task<Chat> GetChat(Guid id);

    /// <summary>
    /// Create a new group chat
    /// </summary>
    Task<Chat> CreateChat(CreateChat createChat);

    /// <summary>
    /// Update an existing chat
    /// </summary>
    Task<Chat> UpdateChat(Guid id, UpdateChat updateChat);

    /// <summary>
    /// Check if chat can be started with user
    /// </summary>
    Task<ChatAvailability> CanStartChat(string username);

    /// <summary>
    /// Get single conversation
    /// </summary>
    /// <param name="id">Conversation identifier</param>
    /// <returns></returns>
    Task<Envelope<Conversation>> GetConversation(Guid id);

    /// <summary>
    /// Get or create direct conversation with user by login
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns></returns>
    Task<Envelope<Conversation>> GetDirectConversation(string login);

    /// <summary>
    /// Get list of conversation messages with offset-based paging (legacy)
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="query">Paging query</param>
    /// <returns></returns>
    Task<ListEnvelope<Message>> GetMessages(Guid conversationId, PagingQuery query);

    /// <summary>
    /// Get list of conversation messages with cursor-based pagination
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="cursor">Opaque cursor for pagination</param>
    /// <param name="aroundMessageId">Get messages around this message ID</param>
    /// <param name="nearTimestampUtc">Get messages near this UTC timestamp</param>
    /// <param name="limit">Maximum number of messages to return</param>
    /// <returns></returns>
    Task<CursorEnvelope<Message>> GetMessagesWithCursor(
        Guid conversationId,
        string? cursor = null,
        Guid? aroundMessageId = null,
        DateTimeOffset? nearTimestampUtc = null,
        int limit = 50);

    /// <summary>
    /// Create new message
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="message">Message</param>
    /// <returns></returns>
    Task<Envelope<Message>> CreateMessage(Guid conversationId, Message message);

    /// <summary>
    /// Get single message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <returns></returns>
    Task<Envelope<Message>> GetMessage(Guid messageId);

    /// <summary>
    /// Update existing message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <param name="message">Message data</param>
    /// <returns></returns>
    Task<Envelope<Message>> UpdateMessage(Guid messageId, Message message);

    /// <summary>
    /// Delete single message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <returns></returns>
    Task DeleteMessage(Guid messageId);

    /// <summary>
    /// Mark all conversation messages as read
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <returns></returns>
    Task MarkAsRead(Guid conversationId);

    /// <summary>
    /// Like a message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <returns></returns>
    Task<Envelope<Message>> LikeMessage(Guid messageId);

    /// <summary>
    /// Unlike a message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <returns></returns>
    Task UnlikeMessage(Guid messageId);

    /// <summary>
    /// Create a new group conversation
    /// </summary>
    /// <param name="createConversation">Conversation data</param>
    /// <returns></returns>
    Task<Envelope<Conversation>> CreateConversation(CreateConversation createConversation);

    /// <summary>
    /// Update an existing conversation
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="updateConversation">Update data</param>
    /// <returns></returns>
    Task<Envelope<Conversation>> UpdateConversation(Guid conversationId, UpdateConversation updateConversation);
}
