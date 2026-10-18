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
using ServiceCreateChat = DM.Domain.Messaging.Features.Chats.CreateChat;
using ServiceUpdateChat = DM.Domain.Messaging.Features.Chats.UpdateChat;
using ServiceCreateMessage = DM.Domain.Messaging.Features.Messages.CreateMessage;
using ServiceUpdateMessage = DM.Domain.Messaging.Features.Messages.UpdateMessage;
using ServiceMessage = DM.Domain.Messaging.Features.Messages.Message;
using ApiChat = DM.Web.API.Features.Messaging.Chats.Chat;
using ApiChatViewer = DM.Web.API.Features.Messaging.Chats.ChatViewer;
using DomainChat = DM.Domain.Messaging.Features.Chats.Chat;
using ApiCreateChat = DM.Web.API.Features.Messaging.Chats.CreateChat;
using ApiUpdateChat = DM.Web.API.Features.Messaging.Chats.UpdateChat;
using ApiChatUnreadCount = DM.Web.API.Features.Messaging.Chats.ChatUnreadCount;
using ApiMessage = DM.Web.API.Features.Messaging.Messages.Message;

namespace DM.Web.API.Features.Messaging;

/// <inheritdoc />
internal class MessagingApiService : IMessagingApiService
{
    private readonly IChatService _chatService;
    private readonly IMessageService _messageService;
    private readonly IMessageLikeService _messageLikeService;
    private readonly MessagingMapper _mapper;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<ListEnvelope<ApiMessage>> GetMessagesAsync(Guid chatId, PagingQuery query)
    {
        var cursorQuery = new CursorQuery
        {
            Cursor = null,
            Limit = query.Take
        };

        var result = await _messageService.GetWithCursorAsync(chatId, cursorQuery);

        // Note: Cursor-based pagination doesn't support traditional paging info
        // This is a compatibility shim for legacy API
        var pagingResult = PagingResult.Empty(query.Take);
        var pagingInfo = new PagingInfo(pagingResult);

        return new ListEnvelope<ApiMessage>(result.Data.Select(_mapper.ToMessage), pagingInfo);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
        new(result.Data.Select(_mapper.ToMessage), new CursorPaging
        {
            NextCursor = result.NextCursor,
            PrevCursor = result.PrevCursor,
            HasNext = result.HasNext,
            HasPrev = result.HasPrev
        });

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> CreateMessageAsync(Guid chatId, ApiMessage message)
    {
        var createMessage = _mapper.ToCreateMessage(message);
        createMessage.ChatId = chatId;
        var createdMessage = await _messageService.CreateAsync(createMessage);
        return new Envelope<ApiMessage>(_mapper.ToMessage(createdMessage));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> CreateGameRoomMessageAsync(Guid chatId, ApiMessage message)
    {
        var createMessage = _mapper.ToCreateMessage(message);
        createMessage.ChatId = chatId;
        var createdMessage = await _messageService.CreateInGameRoomAsync(createMessage);
        return new Envelope<ApiMessage>(_mapper.ToMessage(createdMessage));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> GetMessageAsync(Guid messageId)
    {
        var message = await _messageService.GetAsync(messageId);
        return new Envelope<ApiMessage>(_mapper.ToMessage(message));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> UpdateMessageAsync(Guid messageId, ApiMessage message)
    {
        var updateMessage = _mapper.ToUpdateMessage(message);
        updateMessage.MessageId = messageId;
        var updatedMessage = await _messageService.UpdateAsync(updateMessage);
        return new Envelope<ApiMessage>(_mapper.ToMessage(updatedMessage));
    }

    /// <inheritdoc />
    public Task DeleteMessageAsync(Guid messageId) => _messageService.DeleteAsync(messageId);

    /// <inheritdoc />
    public Task MarkAsReadAsync(Guid chatId) => _chatService.MarkAsReadAsync(chatId);

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> LikeMessageAsync(Guid messageId)
    {
        await _messageLikeService.LikeMessageAsync(messageId);
        return await GetMessageAsync(messageId);
    }

    /// <inheritdoc />
    public Task UnlikeMessageAsync(Guid messageId) => _messageLikeService.UnlikeMessageAsync(messageId);

    /// <inheritdoc />
    public async Task<(IEnumerable<ApiChat> Chats, PagingInfo Paging)> GetChatsAsync(PagingQuery query)
    {
        var (chats, paging) = await _chatService.GetAsync(query);
        return (chats.Select(_mapper.ToChat), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ApiChat> GetDirectChatAsync(string username)
    {
        var chat = await _chatService.GetOrCreateDirectAsync(username);
        return await ChatForViewer(chat);
    }

    /// <inheritdoc />
    public async Task<ApiChat> GetChatAsync(Guid id)
    {
        var chat = await _chatService.GetAsync(id);
        return await ChatForViewer(chat);
    }

    /// <inheritdoc />
    public async Task<ApiChat> GetChatByPublicIdAsync(string publicId)
    {
        var chat = await _chatService.GetByPublicIdAsync(publicId);
        return await ChatForViewer(chat);
    }

    /// <inheritdoc />
    public async Task<Guid> ResolveChatIdAsync(string idOrPublicId) =>
        Guid.TryParse(idOrPublicId, out var guid)
            ? guid
            : (await _chatService.GetByPublicIdAsync(idOrPublicId)).Id;

    /// <inheritdoc />
    public async Task<ApiChat> CreateChatAsync(ApiCreateChat createChat)
    {
        var serviceCreateChat = _mapper.ToCreateChat(createChat);
        var chat = await _chatService.CreateGroupAsync(serviceCreateChat);
        return await ChatForViewer(chat);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task LeaveChatAsync(Guid id) => _chatService.LeaveAsync(id);

    /// <inheritdoc />
    public Task ClearChatHistoryAsync(Guid id) => _chatService.ClearHistoryAsync(id);

    /// <inheritdoc />
    public async Task<Envelope<ApiChatUnreadCount>> GetUnreadCountAsync() =>
        new(new ApiChatUnreadCount { Count = await _chatService.CountUnreadAsync() });

    // ═══ GLOBAL CHAT ═══

    /// <inheritdoc />
    public Task<CursorEnvelope<ApiMessage>> GetGlobalChatMessagesAsync(
        string? cursor = null,
        Guid? aroundMessageId = null,
        DateTimeOffset? nearTimestampUtc = null,
        int limit = 50) =>
        GetMessagesWithCursorAsync(
            WellKnownChats.GlobalChatId, cursor, aroundMessageId, nearTimestampUtc, limit);

    /// <inheritdoc />
    public Task<Envelope<ApiMessage>> CreateGlobalChatMessageAsync(ApiMessage message) =>
        CreateMessageAsync(WellKnownChats.GlobalChatId, message);

    /// <inheritdoc />
    public Task MarkGlobalChatAsReadAsync() =>
        MarkAsReadAsync(WellKnownChats.GlobalChatId);

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> GetGlobalChatMessageAsync(Guid messageId)
    {
        var message = await _messageService.GetGlobalChatMessageAsync(messageId);
        return new Envelope<ApiMessage>(_mapper.ToMessage(message));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> UpdateGlobalChatMessageAsync(Guid messageId, ApiMessage message)
    {
        var updateMessage = _mapper.ToUpdateMessage(message);
        updateMessage.MessageId = messageId;
        var updatedMessage = await _messageService.UpdateGlobalChatMessageAsync(updateMessage);
        return new Envelope<ApiMessage>(_mapper.ToMessage(updatedMessage));
    }

    /// <inheritdoc />
    public Task DeleteGlobalChatMessageAsync(Guid messageId) =>
        _messageService.DeleteGlobalChatMessageAsync(messageId);

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> LikeGlobalChatMessageAsync(Guid messageId)
    {
        await _messageLikeService.LikeGlobalChatMessageAsync(messageId);
        return await GetGlobalChatMessageAsync(messageId);
    }

    /// <inheritdoc />
    public Task UnlikeGlobalChatMessageAsync(Guid messageId) =>
        _messageLikeService.UnlikeGlobalChatMessageAsync(messageId);
}
