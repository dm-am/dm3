using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Messages;

/// <summary>
/// Unified repository for message operations
/// </summary>
public interface IMessageRepository
{
    // ═══ READ ═══

    /// <summary>
    /// Get single message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Message or null</returns>
    Task<Message?> Get(Guid messageId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get single global chat message
    /// </summary>
    /// <remarks>
    /// Takes no reader, and deliberately so. The read above asks whether the
    /// reader is a participant of the chat, which is the whole rule of private
    /// correspondence; the global chat has no participants, because the right to
    /// read it belongs to every visitor by definition. Asking the same question
    /// of it answered "no" for everybody, the author included.
    ///
    /// What it does ask is the chat type, and that is what keeps this from being
    /// an unfiltered read of any message by identifier: a private one addressed
    /// through here is not found.
    /// </remarks>
    /// <param name="messageId">Message identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Message or null</returns>
    Task<Message?> GetGlobalChatMessage(Guid messageId, CancellationToken ct = default);

    /// <summary>
    /// Get messages with cursor-based pagination
    /// </summary>
    /// <param name="chatId">Chat identifier</param>
    /// <param name="query">Cursor query parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cursor result with messages and pagination info</returns>
    Task<CursorResult<Message>> GetWithCursor(Guid chatId, CursorQuery query, CancellationToken ct = default);

    // ═══ WRITE ═══

    /// <summary>
    /// Save message
    /// </summary>
    /// <param name="message">Message data</param>
    /// <param name="updateChat">Chat update data for last message</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created message</returns>
    Task<Message> Create(CreateMessageEntity message, UpdateChatLastMessageEntity updateChat, CancellationToken ct = default);

    /// <summary>
    /// Update single message
    /// </summary>
    /// <param name="update">Update data</param>
    /// <returns>Updated message</returns>
    Task<Message> Update(UpdateMessageEntity update);

    /// <summary>
    /// Delete message (soft delete)
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <param name="deletedByUserId">User who deleted the message</param>
    /// <param name="ct">Cancellation token</param>
    Task Delete(Guid messageId, Guid deletedByUserId, CancellationToken ct = default);
}
