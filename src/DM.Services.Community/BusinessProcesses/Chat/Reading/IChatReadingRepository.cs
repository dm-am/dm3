using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Chat.Reading;

/// <summary>
/// Storage for reading chat messages
/// </summary>
internal interface IChatReadingRepository
{
    /// <summary>
    /// Count chat messages
    /// </summary>
    /// <returns></returns>
    Task<int> Count();

    /// <summary>
    /// Get chat messages
    /// </summary>
    /// <param name="pagingData"></param>
    /// <returns></returns>
    Task<IEnumerable<ChatMessage>> Get(PagingData pagingData);

    /// <summary>
    /// Get new messages since given moment
    /// </summary>
    /// <param name="since"></param>
    /// <returns></returns>
    Task<IEnumerable<ChatMessage>> Get(DateTimeOffset since);

    /// <summary>
    /// Get single chat message
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<ChatMessage> Get(Guid id);

    /// <summary>
    /// Get chat messages for a specific date
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    Task<IEnumerable<ChatMessage>> GetByDate(DateOnly date);

    /// <summary>
    /// Get messages before (older than) the given message
    /// </summary>
    /// <param name="messageId">Reference message ID</param>
    /// <param name="count">Number of messages to fetch</param>
    /// <returns>Messages ordered by date ascending</returns>
    Task<IEnumerable<ChatMessage>> GetBefore(Guid messageId, int count);

    /// <summary>
    /// Get messages after (newer than) the given message
    /// </summary>
    /// <param name="messageId">Reference message ID</param>
    /// <param name="count">Number of messages to fetch</param>
    /// <returns>Messages ordered by date ascending</returns>
    Task<IEnumerable<ChatMessage>> GetAfter(Guid messageId, int count);

    /// <summary>
    /// Get messages around the given message (half before, half after)
    /// </summary>
    /// <param name="messageId">Reference message ID</param>
    /// <param name="count">Total number of messages to fetch</param>
    /// <returns>Messages ordered by date ascending</returns>
    Task<IEnumerable<ChatMessage>> GetAround(Guid messageId, int count);

    /// <summary>
    /// Check if there are messages before the given message
    /// </summary>
    Task<bool> HasMessagesBefore(Guid messageId);

    /// <summary>
    /// Check if there are messages after the given message
    /// </summary>
    Task<bool> HasMessagesAfter(Guid messageId);

    /// <summary>
    /// Get first message on or after the given date
    /// </summary>
    Task<ChatMessage> GetFirstOnOrAfterDate(DateOnly date);

    /// <summary>
    /// Get last message on or before the given date
    /// </summary>
    Task<ChatMessage> GetLastOnOrBeforeDate(DateOnly date);
}