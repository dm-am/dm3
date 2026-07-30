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

    /// <summary>
    /// Create new message in a game room chat
    /// </summary>
    /// <remarks>
    /// Separate from the general create on purpose. A game room chat has no
    /// participants of its own, so the participation rule the general create
    /// applies refuses everyone; the game module authorizes the room instead and
    /// then calls this. Keeping it a distinct entry point is what stops the chat
    /// endpoints from reaching a game room chat without that room check.
    /// </remarks>
    /// <param name="createMessage">DTO model</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created message</returns>
    Task<Message> CreateInGameRoomAsync(CreateMessage createMessage, CancellationToken ct = default);

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

    /// <summary>
    /// Get list of game room chat messages with cursor-based pagination
    /// </summary>
    /// <remarks>
    /// The room decides who reads these messages, not the chat participants, and
    /// the game module has already checked it by the time this is called.
    /// </remarks>
    /// <param name="chatId">Chat identifier</param>
    /// <param name="query">Cursor query parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cursor result with messages and pagination info</returns>
    Task<CursorResult<Message>> GetGameRoomWithCursorAsync(Guid chatId, CursorQuery query, CancellationToken ct = default);

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
