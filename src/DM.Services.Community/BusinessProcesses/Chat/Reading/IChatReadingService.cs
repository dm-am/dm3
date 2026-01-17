using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Chat.Reading;

/// <summary>
/// Service for reading chat messages
/// </summary>
public interface IChatReadingService
{
    /// <summary>
    /// Get list of chat messages
    /// </summary>
    /// <returns></returns>
    Task<(IEnumerable<ChatMessage> messages, PagingResult paging)> GetMessages(PagingQuery pagingQuery);

    /// <summary>
    /// Get list of new chat messages since given moment
    /// </summary>
    /// <param name="since"></param>
    /// <returns></returns>
    Task<IEnumerable<ChatMessage>> GetNewMessages(DateTimeOffset since);

    /// <summary>
    /// Get single chat message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <returns></returns>
    Task<ChatMessage> GetMessage(Guid id);

    /// <summary>
    /// Get chat messages for a specific date
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    Task<IEnumerable<ChatMessage>> GetMessagesByDate(DateOnly date);

    /// <summary>
    /// Get messages before (older than) the given message
    /// </summary>
    /// <param name="messageId">Reference message ID</param>
    /// <param name="count">Number of messages to fetch</param>
    /// <returns>Messages with hasMore flags</returns>
    Task<(IEnumerable<ChatMessage> messages, bool hasMoreBefore)> GetMessagesBefore(Guid messageId, int count);

    /// <summary>
    /// Get messages after (newer than) the given message
    /// </summary>
    /// <param name="messageId">Reference message ID</param>
    /// <param name="count">Number of messages to fetch</param>
    /// <returns>Messages with hasMore flags</returns>
    Task<(IEnumerable<ChatMessage> messages, bool hasMoreAfter)> GetMessagesAfter(Guid messageId, int count);

    /// <summary>
    /// Get messages around the given message
    /// </summary>
    /// <param name="messageId">Reference message ID</param>
    /// <param name="count">Total number of messages to fetch</param>
    /// <returns>Messages with hasMore flags for both directions</returns>
    Task<(IEnumerable<ChatMessage> messages, bool hasMoreBefore, bool hasMoreAfter)> GetMessagesAround(Guid messageId, int count);

    /// <summary>
    /// Get first message on or after the given date
    /// </summary>
    /// <param name="date">Date to search from</param>
    /// <returns>First message or null</returns>
    Task<ChatMessage> GetFirstMessageOnOrAfterDate(DateOnly date);
}