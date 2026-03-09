using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Messages;

/// <summary>
/// Unified service for message operations
/// </summary>
public interface IMessageService
{
    // ═══ CREATE ═══

    /// <summary>
    /// Create new message
    /// </summary>
    /// <param name="createMessage">DTO model</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created message</returns>
    Task<Message> CreateAsync(CreateMessage createMessage, CancellationToken ct = default);

    // ═══ READ ═══

    /// <summary>
    /// Get single message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Message</returns>
    Task<Message> GetAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>
    /// Get list of chat messages with cursor-based pagination
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    /// <param name="query">Cursor query parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cursor result with messages and pagination info</returns>
    Task<CursorResult<Message>> GetWithCursorAsync(Guid chatId, CursorQuery query, CancellationToken ct = default);

    // ═══ UPDATE ═══

    /// <summary>
    /// Update existing message
    /// </summary>
    /// <param name="updateMessage">Update message model</param>
    /// <returns>Updated message</returns>
    Task<Message> UpdateAsync(UpdateMessage updateMessage);

    // ═══ DELETE ═══

    /// <summary>
    /// Delete message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    Task DeleteAsync(Guid messageId);
}
