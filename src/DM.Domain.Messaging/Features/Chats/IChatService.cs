using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Chats;

public interface IChatService
{
    /// <summary>
    /// Create a group chat; refused whole when an invited participant keeps the author
    /// on a blacklist with private messages blocked
    /// </summary>
    Task<Chat> CreateGroupAsync(CreateChat createChat);

    Task<Chat> CreateGameRoomChatAsync(Guid roomId, string title);

    Task<(IEnumerable<Chat> Chats, PagingResult Paging)> GetAsync(PagingQuery query);

    Task<Chat> GetAsync(Guid chatId);

    /// <summary>
    /// Get a game room chat without the participation check: it has no participant rows,
    /// and the game module decides access to its room before this call. Any other chat
    /// is not found here.
    /// </summary>
    Task<Chat> GetGameRoomAsync(Guid chatId);

    /// <summary>
    /// Get a chat by its public identifier (5 letters)
    /// </summary>
    Task<Chat> GetByPublicIdAsync(string publicId);

    /// <summary>
    /// Find or create the direct chat with the user
    /// </summary>
    Task<Chat> GetOrCreateDirectAsync(string username);

    /// <summary>
    /// Count unread messages of the current user across every conversation: one aggregate
    /// over the markers parented by the reader (IUnreadCountersRepository)
    /// </summary>
    Task<long> CountUnreadAsync();

    Task MarkAsReadAsync(Guid chatId);

    /// <summary>
    /// Update the title and the participants; somebody who keeps the caller on a blacklist
    /// with private messages blocked is not added
    /// </summary>
    Task<Chat> UpdateAsync(UpdateChat updateChat);

    /// <summary>
    /// Leave a group chat: it goes out of the leaver's list with his unread marker, the
    /// others keep it with every message. A direct chat is not left.
    /// </summary>
    Task LeaveAsync(Guid chatId);

    /// <summary>
    /// Clear the history of a direct or group chat for the current user: nothing is deleted,
    /// the messages up to now are no longer given to him and count as read
    /// </summary>
    Task ClearHistoryAsync(Guid chatId);

    /// <summary>
    /// Whether the current user may leave the chat read for him: true exactly when
    /// <see cref="LeaveAsync" /> takes it
    /// </summary>
    bool MayLeave(Chat chat);

    /// <summary>
    /// Whether the current user may clear the history of the chat read for him: true
    /// exactly when <see cref="ClearHistoryAsync" /> takes it
    /// </summary>
    bool MayClearHistory(Chat chat);

    Task DeleteAsync(Guid chatId);
}
