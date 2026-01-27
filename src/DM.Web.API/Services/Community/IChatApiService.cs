using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Messaging;

namespace DM.Web.API.Services.Community;

/// <summary>
/// API service for chat
/// </summary>
public interface IChatApiService
{
    /// <summary>
    /// Get list of chat messages
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns></returns>
    Task<ListEnvelope<ChatMessage>> GetMessages(PagingQuery query);

    /// <summary>
    /// Create new chat message
    /// </summary>
    /// <param name="message">Message</param>
    /// <returns></returns>
    Task<Envelope<ChatMessage>> CreateMessage(ChatMessage message);

    /// <summary>
    /// Get single chat message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <returns></returns>
    Task<Envelope<ChatMessage>> GetMessage(Guid id);

    /// <summary>
    /// Update chat message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <param name="message">Message data</param>
    /// <returns></returns>
    Task<Envelope<ChatMessage>> UpdateMessage(Guid id, ChatMessage message);

    /// <summary>
    /// Delete chat message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <returns></returns>
    Task DeleteMessage(Guid id);

    /// <summary>
    /// Like chat message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <returns></returns>
    Task<Envelope<ChatMessage>> LikeMessage(Guid id);

    /// <summary>
    /// Unlike chat message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <returns></returns>
    Task<Envelope<ChatMessage>> UnlikeMessage(Guid id);

    /// <summary>
    /// Get chat messages for a specific date
    /// </summary>
    /// <param name="date">Date</param>
    /// <returns></returns>
    Task<ListEnvelope<ChatMessage>> GetMessagesByDate(DateOnly date);

    /// <summary>
    /// Get messages before (older than) the given message
    /// </summary>
    /// <param name="messageId">Reference message ID</param>
    /// <param name="count">Number of messages to fetch</param>
    /// <returns></returns>
    Task<ListEnvelope<ChatMessage>> GetMessagesBefore(Guid messageId, int count);

    /// <summary>
    /// Get messages after (newer than) the given message
    /// </summary>
    /// <param name="messageId">Reference message ID</param>
    /// <param name="count">Number of messages to fetch</param>
    /// <returns></returns>
    Task<ListEnvelope<ChatMessage>> GetMessagesAfter(Guid messageId, int count);

    /// <summary>
    /// Get messages around the given message
    /// </summary>
    /// <param name="messageId">Reference message ID</param>
    /// <param name="count">Total number of messages to fetch</param>
    /// <returns></returns>
    Task<ListEnvelope<ChatMessage>> GetMessagesAround(Guid messageId, int count);

    /// <summary>
    /// Get first message on or after the given date
    /// </summary>
    /// <param name="date">Date to search from</param>
    /// <returns></returns>
    Task<Envelope<ChatMessage>> GetFirstMessageOnOrAfterDate(DateOnly date);

    /// <summary>
    /// Get last message on or before the given date
    /// </summary>
    /// <param name="date">Date to search until</param>
    /// <returns></returns>
    Task<Envelope<ChatMessage>> GetLastMessageOnOrBeforeDate(DateOnly date);
}