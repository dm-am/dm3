using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Messaging.Features.Chats;

namespace DM.Domain.Messaging.Features.Messages;

public interface IMessageService
{
    Task<Message> CreateAsync(CreateMessage createMessage, CancellationToken ct = default);

    /// <summary>
    /// Create a message in a game room chat. The chat has no participants, so the game
    /// module authorizes the room and calls this; the chat endpoints cannot reach it.
    /// </summary>
    Task<Message> CreateInGameRoomAsync(CreateMessage createMessage, CancellationToken ct = default);

    /// <summary>
    /// Whether the current user may write a new message in the chat read for him: true
    /// exactly when <see cref="CreateAsync" /> takes one there, the text aside
    /// </summary>
    Task<bool> MayCreateAsync(Chat chat, CancellationToken ct = default);

    /// <summary>
    /// Whether the current user may write to this user in private: a conversation of the
    /// two of them would take his message. False for a guest and for the user himself.
    /// </summary>
    Task<bool> MayWriteToAsync(Guid userId, CancellationToken ct = default);

    Task<Message> GetAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>
    /// Get a global chat message, readable by everyone, signed in or not
    /// </summary>
    Task<Message> GetGlobalChatMessageAsync(Guid messageId, CancellationToken ct = default);

    Task<CursorResult<Message>> GetWithCursorAsync(Guid chatId, CursorQuery query, CancellationToken ct = default);

    /// <summary>
    /// Read a game room chat, whose room the game module has already checked
    /// </summary>
    Task<CursorResult<Message>> GetGameRoomWithCursorAsync(Guid chatId, CursorQuery query, CancellationToken ct = default);

    Task<Message> UpdateAsync(UpdateMessage updateMessage);

    /// <summary>
    /// Update a global chat message, by <see cref="Authorization.MessageIntention.Edit"/>
    /// </summary>
    Task<Message> UpdateGlobalChatMessageAsync(UpdateMessage updateMessage);

    Task DeleteAsync(Guid messageId);

    /// <summary>
    /// Delete a global chat message, by <see cref="Authorization.MessageIntention.Delete"/>
    /// </summary>
    Task DeleteGlobalChatMessageAsync(Guid messageId);
}
