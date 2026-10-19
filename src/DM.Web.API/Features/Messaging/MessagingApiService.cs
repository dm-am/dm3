using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.Messages;
using DM.Domain.Messaging.Features.Likes;
using DM.Domain.Core.Chats;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using ServiceMessage = DM.Domain.Messaging.Features.Messages.Message;
using ApiChat = DM.Web.API.Features.Messaging.Chats.Chat;
using ApiChatViewer = DM.Web.API.Features.Messaging.Chats.ChatViewer;
using DomainChat = DM.Domain.Messaging.Features.Chats.Chat;
using ApiCreateChat = DM.Web.API.Features.Messaging.Chats.CreateChat;
using ApiUpdateChat = DM.Web.API.Features.Messaging.Chats.UpdateChat;

using ApiMessage = DM.Web.API.Features.Messaging.Messages.Message;

namespace DM.Web.API.Features.Messaging;

public class MessagingApiService
{
    private readonly IChatService _chatService;
    private readonly IMessageService _messageService;
    private readonly IMessageLikeService _messageLikeService;
    private readonly MessagingMapper _mapper;

    public MessagingApiService(
        IChatService chatService,
        IMessageService messageService,
        IMessageLikeService messageLikeService,
        MessagingMapper mapper)
    {
        _chatService = chatService;
        _messageService = messageService;
        _messageLikeService = messageLikeService;
        _mapper = mapper;
    }

    public async Task<CursorEnvelope<ApiMessage>> GetMessagesWithCursorAsync(
        Guid chatId,
        string? cursor = null,
        Guid? aroundMessageId = null,
        DateTimeOffset? nearTimestampUtc = null,
        int limit = 50)
    {
        var cursorQuery = new CursorQuery
        {
            Cursor = cursor,
            AroundEntityId = aroundMessageId,
            NearTimestampUtc = nearTimestampUtc,
            Limit = limit
        };

        var result = await _messageService.GetWithCursorAsync(chatId, cursorQuery);
        return ToCursorEnvelope(result);
    }

    /// <summary>Authorizes by the room, for the game chat room endpoints; the chat endpoints keep the general reader above.</summary>
    public async Task<CursorEnvelope<ApiMessage>> GetGameRoomMessagesWithCursorAsync(
        Guid chatId,
        string? cursor = null,
        int limit = 50)
    {
        var cursorQuery = new CursorQuery
        {
            Cursor = cursor,
            Limit = limit
        };

        var result = await _messageService.GetGameRoomWithCursorAsync(chatId, cursorQuery);
        return ToCursorEnvelope(result);
    }

    private CursorEnvelope<ApiMessage> ToCursorEnvelope(CursorResult<ServiceMessage> result) =>
        new(result.Data.Select(_mapper.ToMessage), CursorPaging.Of(result));

    public async Task<Envelope<ApiMessage>> CreateMessageAsync(Guid chatId, string text)
    {
        var createdMessage = await _messageService.CreateAsync(new CreateMessage { ChatId = chatId, Text = text });
        return new Envelope<ApiMessage>(_mapper.ToMessage(createdMessage));
    }

    public async Task<Envelope<ApiMessage>> CreateGameRoomMessageAsync(Guid chatId, string text)
    {
        var createdMessage = await _messageService.CreateInGameRoomAsync(
            new CreateMessage { ChatId = chatId, Text = text });
        return new Envelope<ApiMessage>(_mapper.ToMessage(createdMessage));
    }

    public async Task<Envelope<ApiMessage>> GetMessageAsync(Guid messageId)
    {
        var message = await _messageService.GetAsync(messageId);
        return new Envelope<ApiMessage>(_mapper.ToMessage(message));
    }

    public async Task<Envelope<ApiMessage>> UpdateMessageAsync(Guid messageId, string text)
    {
        var updatedMessage = await _messageService.UpdateAsync(new UpdateMessage { MessageId = messageId, Text = text });
        return new Envelope<ApiMessage>(_mapper.ToMessage(updatedMessage));
    }

    public Task DeleteMessageAsync(Guid messageId) => _messageService.DeleteAsync(messageId);

    public Task MarkAsReadAsync(Guid chatId) => _chatService.MarkAsReadAsync(chatId);

    public async Task<Envelope<ApiMessage>> LikeMessageAsync(Guid messageId)
    {
        await _messageLikeService.LikeMessageAsync(messageId);
        return await GetMessageAsync(messageId);
    }

    public Task UnlikeMessageAsync(Guid messageId) => _messageLikeService.UnlikeMessageAsync(messageId);

    public async Task<(IEnumerable<ApiChat> Chats, PagingInfo Paging)> GetChatsAsync(PagingQuery query)
    {
        var (chats, paging) = await _chatService.GetAsync(query);
        return (chats.Select(_mapper.ToChat), new PagingInfo(paging));
    }

    public async Task<ApiChat> GetDirectChatAsync(string username)
    {
        var chat = await _chatService.GetOrCreateDirectAsync(username);
        return await ChatForViewer(chat);
    }

    public async Task<ApiChat> GetChatAsync(Guid id)
    {
        var chat = await _chatService.GetAsync(id);
        return await ChatForViewer(chat);
    }

    public async Task<ApiChat> GetChatByPublicIdAsync(string publicId)
    {
        var chat = await _chatService.GetByPublicIdAsync(publicId);
        return await ChatForViewer(chat);
    }

    /// <summary>Every chat-scoped route accepts either form, so the branch lives here once rather than in each controller.</summary>
    public async Task<Guid> ResolveChatIdAsync(string idOrPublicId) =>
        Guid.TryParse(idOrPublicId, out var guid)
            ? guid
            : (await _chatService.GetByPublicIdAsync(idOrPublicId)).Id;

    public async Task<ApiChat> CreateChatAsync(ApiCreateChat createChat)
    {
        var serviceCreateChat = _mapper.ToCreateChat(createChat);
        var chat = await _chatService.CreateGroupAsync(serviceCreateChat);
        return await ChatForViewer(chat);
    }

    public async Task<ApiChat> UpdateChatAsync(Guid id, ApiUpdateChat updateChat)
    {
        var serviceUpdateChat = _mapper.ToUpdateChat(updateChat);
        serviceUpdateChat.ChatId = id;
        var chat = await _chatService.UpdateAsync(serviceUpdateChat);
        return await ChatForViewer(chat);
    }

    /// <summary>
    /// One chat as the viewer gets it: the chat, and what he may do in it, asked
    /// of the code that refuses the message. A list leaves the second half out,
    /// so the reads of one chat and the writes that return one come through here.
    /// </summary>
    private async Task<ApiChat> ChatForViewer(DomainChat chat)
    {
        var result = _mapper.ToChat(chat);
        result.Viewer = new ApiChatViewer
        {
            CanWrite = await _messageService.MayCreateAsync(chat),
            CanLeave = _chatService.MayLeave(chat),
            CanClearHistory = _chatService.MayClearHistory(chat)
        };
        return result;
    }

    public Task LeaveChatAsync(Guid id) => _chatService.LeaveAsync(id);

    public Task ClearChatHistoryAsync(Guid id) => _chatService.ClearHistoryAsync(id);

    public async Task<Envelope<UnreadCount>> GetUnreadCountAsync() =>
        new(new UnreadCount { Count = await _chatService.CountUnreadAsync() });

    public Task<CursorEnvelope<ApiMessage>> GetGlobalChatMessagesAsync(
        string? cursor = null,
        Guid? aroundMessageId = null,
        DateTimeOffset? nearTimestampUtc = null,
        int limit = 50) =>
        GetMessagesWithCursorAsync(
            WellKnownChats.GlobalChatId, cursor, aroundMessageId, nearTimestampUtc, limit);

    public Task<Envelope<ApiMessage>> CreateGlobalChatMessageAsync(string text) =>
        CreateMessageAsync(WellKnownChats.GlobalChatId, text);

    public Task MarkGlobalChatAsReadAsync() =>
        MarkAsReadAsync(WellKnownChats.GlobalChatId);

    /// <summary>Whoever the global chat is open to may read any line of it; the private-message routes would ask whether the reader takes part in the conversation.</summary>
    public async Task<Envelope<ApiMessage>> GetGlobalChatMessageAsync(Guid messageId)
    {
        var message = await _messageService.GetGlobalChatMessageAsync(messageId);
        return new Envelope<ApiMessage>(_mapper.ToMessage(message));
    }

    public async Task<Envelope<ApiMessage>> UpdateGlobalChatMessageAsync(Guid messageId, string text)
    {
        var updatedMessage = await _messageService.UpdateGlobalChatMessageAsync(
            new UpdateMessage { MessageId = messageId, Text = text });
        return new Envelope<ApiMessage>(_mapper.ToMessage(updatedMessage));
    }

    public Task DeleteGlobalChatMessageAsync(Guid messageId) =>
        _messageService.DeleteGlobalChatMessageAsync(messageId);

    public async Task<Envelope<ApiMessage>> LikeGlobalChatMessageAsync(Guid messageId)
    {
        await _messageLikeService.LikeGlobalChatMessageAsync(messageId);
        return await GetGlobalChatMessageAsync(messageId);
    }

    public Task UnlikeGlobalChatMessageAsync(Guid messageId) =>
        _messageLikeService.UnlikeGlobalChatMessageAsync(messageId);
}
