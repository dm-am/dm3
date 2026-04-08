using System;
using System.Threading.Tasks;
using DM.Web.API.Features.Messaging.Messages;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.ChatRooms;

/// <summary>
/// API service for chat rooms
/// </summary>
public interface IChatRoomApiService
{
    /// <summary>
    /// Get chat rooms in a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task<ListEnvelope<ChatRoom>> GetChatRoomsAsync(Guid gameId);

    /// <summary>
    /// Get single chat room
    /// </summary>
    /// <param name="id">Chat room identifier</param>
    Task<Envelope<ChatRoom>> GetChatRoomAsync(Guid id);

    /// <summary>
    /// Create a new chat room in a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="input">Chat room creation data</param>
    Task<Envelope<ChatRoom>> CreateChatRoomAsync(Guid gameId, CreateChatRoom input);

    /// <summary>
    /// Update an existing chat room
    /// </summary>
    /// <param name="id">Chat room identifier</param>
    /// <param name="input">Updated data</param>
    Task<Envelope<ChatRoom>> UpdateChatRoomAsync(Guid id, UpdateChatRoom input);

    /// <summary>
    /// Delete a chat room
    /// </summary>
    /// <param name="id">Chat room identifier</param>
    Task DeleteChatRoomAsync(Guid id);

    /// <summary>
    /// Get messages in chat room
    /// </summary>
    /// <param name="chatRoomId">Chat room identifier</param>
    /// <param name="cursor">Pagination cursor</param>
    /// <param name="limit">Maximum number of messages to return</param>
    Task<CursorEnvelope<Message>> GetMessagesAsync(Guid chatRoomId, string? cursor, int limit);

    /// <summary>
    /// Create a message in chat room
    /// </summary>
    /// <param name="chatRoomId">Chat room identifier</param>
    /// <param name="input">Message data</param>
    Task<Envelope<Message>> CreateMessageAsync(Guid chatRoomId, CreateMessageInput input);

    /// <summary>
    /// Mark chat room messages as read
    /// </summary>
    /// <param name="chatRoomId">Chat room identifier</param>
    Task MarkAsReadAsync(Guid chatRoomId);
}
