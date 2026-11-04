using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Messages;

public interface IMessageRepository
{
    /// <summary>
    /// The message if the user takes part in its chat, null otherwise
    /// </summary>
    Task<Message?> Get(Guid messageId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// A message of the global chat, null for any other: it takes no reader, because the
    /// global chat has no participants and is read by everybody
    /// </summary>
    Task<Message?> GetGlobalChatMessage(Guid messageId, CancellationToken ct = default);

    Task<CursorResult<Message>> GetWithCursor(Guid chatId, CursorQuery query, CancellationToken ct = default);

    /// <summary>
    /// Save the message and make it the last one of its chat
    /// </summary>
    Task<Message> Create(CreateMessageEntity message, UpdateChatLastMessageEntity updateChat, CancellationToken ct = default);

    Task<Message> Update(UpdateMessageEntity update);

    /// <summary>
    /// Delete the message (soft delete), signed by who deleted it
    /// </summary>
    Task Delete(Guid messageId, Guid deletedByUserId, CancellationToken ct = default);
}
