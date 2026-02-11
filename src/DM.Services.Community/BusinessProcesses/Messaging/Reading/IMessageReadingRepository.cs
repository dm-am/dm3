using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Messaging.Reading;

/// <summary>
/// Storage for reading the messages
/// </summary>
internal interface IMessageReadingRepository
{
    /// <summary>
    /// Count messages in conversation
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<int> Count(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Get messages with offset-based paging (legacy)
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="paging">Paging data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<IEnumerable<Message>> Get(Guid conversationId, PagingData paging, CancellationToken ct = default);

    /// <summary>
    /// Get single message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<Message?> Get(Guid messageId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get messages with cursor-based pagination
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="query">Cursor query parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cursor result with messages and pagination info</returns>
    Task<CursorResult<Message>> GetWithCursor(Guid conversationId, CursorQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get messages around a specific message
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="messageId">Reference message identifier</param>
    /// <param name="limit">Total number of messages to fetch</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cursor result with messages centered around the reference</returns>
    Task<CursorResult<Message>> GetAround(Guid conversationId, Guid messageId, int limit, CancellationToken ct = default);

    /// <summary>
    /// Get messages near a specific timestamp
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="timestampUtc">Reference timestamp (UTC)</param>
    /// <param name="limit">Total number of messages to fetch</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cursor result with messages near the timestamp</returns>
    Task<CursorResult<Message>> GetNearTimestamp(Guid conversationId, DateTimeOffset timestampUtc, int limit, CancellationToken ct = default);

    /// <summary>
    /// Check if there are messages before the given message
    /// </summary>
    Task<bool> HasMessagesBefore(Guid conversationId, Guid messageId, CancellationToken ct = default);

    /// <summary>
    /// Check if there are messages after the given message
    /// </summary>
    Task<bool> HasMessagesAfter(Guid conversationId, Guid messageId, CancellationToken ct = default);
}