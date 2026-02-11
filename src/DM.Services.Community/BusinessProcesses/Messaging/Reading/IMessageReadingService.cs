using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Messaging.Reading;

/// <summary>
/// Service for reading conversation messages
/// </summary>
public interface IMessageReadingService
{
    /// <summary>
    /// Get list of conversation messages with offset-based paging (legacy)
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="query">Paging query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<(IEnumerable<Message> messages, PagingResult paging)> Get(Guid conversationId, PagingQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get single message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<Message> Get(Guid messageId, CancellationToken ct = default);

    /// <summary>
    /// Get list of conversation messages with cursor-based pagination
    /// </summary>
    /// <param name="conversationId">Conversation identifier</param>
    /// <param name="query">Cursor query parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cursor result with messages and pagination info</returns>
    Task<CursorResult<Message>> GetWithCursor(Guid conversationId, CursorQuery query, CancellationToken ct = default);
}