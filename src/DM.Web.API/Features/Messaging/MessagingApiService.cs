using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.Messages;
using DM.Domain.Messaging.Features.Likes;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Features.Blacklists;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using ServiceCreateChat = DM.Domain.Messaging.Features.Chats.CreateChat;
using ServiceUpdateChat = DM.Domain.Messaging.Features.Chats.UpdateChat;
using ServiceCreateMessage = DM.Domain.Messaging.Features.Messages.CreateMessage;
using ServiceUpdateMessage = DM.Domain.Messaging.Features.Messages.UpdateMessage;
using ApiChat = DM.Web.API.Features.Messaging.Chats.Chat;
using ApiCreateChat = DM.Web.API.Features.Messaging.Chats.CreateChat;
using ApiUpdateChat = DM.Web.API.Features.Messaging.Chats.UpdateChat;
using ApiChatAvailability = DM.Web.API.Features.Messaging.Chats.ChatAvailability;
using ApiConversation = DM.Web.API.Features.Messaging.Conversations.Conversation;
using ApiCreateConversation = DM.Web.API.Features.Messaging.Conversations.CreateConversation;
using ApiUpdateConversation = DM.Web.API.Features.Messaging.Conversations.UpdateConversation;
using ApiMessage = DM.Web.API.Features.Messaging.Messages.Message;

namespace DM.Web.API.Features.Messaging;

/// <inheritdoc />
internal class MessagingApiService : IMessagingApiService
{
    private readonly IChatService _chatService;
    private readonly IMessageService _messageService;
    private readonly IMessageLikeService _messageLikeService;
    private readonly IUserService _userService;
    private readonly IUserBlacklistService _userBlacklistService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public MessagingApiService(
        IChatService chatService,
        IMessageService messageService,
        IMessageLikeService messageLikeService,
        IUserService userService,
        IUserBlacklistService userBlacklistService,
        IMapper mapper)
    {
        _chatService = chatService;
        _messageService = messageService;
        _messageLikeService = messageLikeService;
        _userService = userService;
        _userBlacklistService = userBlacklistService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ApiConversation>> GetConversations(PagingQuery query)
    {
        var (conversations, paging) = await _chatService.GetAsync(query);
        return new ListEnvelope<ApiConversation>(conversations.Select(_mapper.Map<ApiConversation>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiConversation>> GetConversation(Guid id)
    {
        var conversation = await _chatService.GetAsync(id);
        return new Envelope<ApiConversation>(_mapper.Map<ApiConversation>(conversation));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiConversation>> GetDirectConversation(string login)
    {
        var conversation = await _chatService.GetOrCreateDirectAsync(login);
        return new Envelope<ApiConversation>(_mapper.Map<ApiConversation>(conversation));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ApiMessage>> GetMessages(Guid conversationId, PagingQuery query)
    {
        var cursorQuery = new CursorQuery
        {
            Cursor = null,
            Limit = query.Take
        };

        var result = await _messageService.GetWithCursorAsync(conversationId, cursorQuery);

        // Note: Cursor-based pagination doesn't support traditional paging info
        // This is a compatibility shim for legacy API
        var pagingResult = PagingResult.Empty(query.Take);
        var pagingInfo = new PagingInfo(pagingResult);

        return new ListEnvelope<ApiMessage>(result.Data.Select(_mapper.Map<ApiMessage>), pagingInfo);
    }

    /// <inheritdoc />
    public async Task<CursorEnvelope<ApiMessage>> GetMessagesWithCursor(
        Guid conversationId,
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

        var result = await _messageService.GetWithCursorAsync(conversationId, cursorQuery);

        var cursorPaging = new CursorPaging
        {
            NextCursor = result.NextCursor,
            PrevCursor = result.PrevCursor,
            HasNext = result.HasNext,
            HasPrev = result.HasPrev
        };

        return new CursorEnvelope<ApiMessage>(
            result.Data.Select(_mapper.Map<ApiMessage>),
            cursorPaging);
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> CreateMessage(Guid conversationId, ApiMessage message)
    {
        var createMessage = _mapper.Map<ServiceCreateMessage>(message);
        createMessage.ChatId = conversationId;
        var createdMessage = await _messageService.CreateAsync(createMessage);
        return new Envelope<ApiMessage>(_mapper.Map<ApiMessage>(createdMessage));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> GetMessage(Guid messageId)
    {
        var message = await _messageService.GetAsync(messageId);
        return new Envelope<ApiMessage>(_mapper.Map<ApiMessage>(message));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> UpdateMessage(Guid messageId, ApiMessage message)
    {
        var updateMessage = _mapper.Map<ServiceUpdateMessage>(message);
        updateMessage.MessageId = messageId;
        var updatedMessage = await _messageService.UpdateAsync(updateMessage);
        return new Envelope<ApiMessage>(_mapper.Map<ApiMessage>(updatedMessage));
    }

    /// <inheritdoc />
    public Task DeleteMessage(Guid messageId) => _messageService.DeleteAsync(messageId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid conversationId) => _chatService.MarkAsReadAsync(conversationId);

    /// <inheritdoc />
    public async Task<Envelope<ApiMessage>> LikeMessage(Guid messageId)
    {
        await _messageLikeService.LikeMessage(messageId);
        return await GetMessage(messageId);
    }

    /// <inheritdoc />
    public Task UnlikeMessage(Guid messageId) => _messageLikeService.UnlikeMessage(messageId);

    /// <inheritdoc />
    public async Task<Envelope<ApiConversation>> CreateConversation(ApiCreateConversation createConversation)
    {
        var serviceCreateConversation = _mapper.Map<ServiceCreateChat>(createConversation);
        var conversation = await _chatService.CreateGroupAsync(serviceCreateConversation);
        return new Envelope<ApiConversation>(_mapper.Map<ApiConversation>(conversation));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiConversation>> UpdateConversation(Guid conversationId, ApiUpdateConversation updateConversation)
    {
        var serviceUpdateConversation = _mapper.Map<ServiceUpdateChat>(updateConversation);
        serviceUpdateConversation.ChatId = conversationId;
        var conversation = await _chatService.UpdateAsync(serviceUpdateConversation);
        return new Envelope<ApiConversation>(_mapper.Map<ApiConversation>(conversation));
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<ApiChat> Chats, PagingInfo Paging)> GetChats(PagingQuery query)
    {
        var (conversations, paging) = await _chatService.GetAsync(query);
        return (conversations.Select(_mapper.Map<ApiChat>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ApiChat> GetDirectChat(string username)
    {
        var conversation = await _chatService.GetOrCreateDirectAsync(username);
        return _mapper.Map<ApiChat>(conversation);
    }

    /// <inheritdoc />
    public async Task<ApiChat> GetChat(Guid id)
    {
        var conversation = await _chatService.GetAsync(id);
        return _mapper.Map<ApiChat>(conversation);
    }

    /// <inheritdoc />
    public async Task<ApiChat> CreateChat(ApiCreateChat createChat)
    {
        var serviceCreateChat = _mapper.Map<ServiceCreateChat>(createChat);
        var conversation = await _chatService.CreateGroupAsync(serviceCreateChat);
        return _mapper.Map<ApiChat>(conversation);
    }

    /// <inheritdoc />
    public async Task<ApiChat> UpdateChat(Guid id, ApiUpdateChat updateChat)
    {
        var serviceUpdateChat = _mapper.Map<ServiceUpdateChat>(updateChat);
        serviceUpdateChat.ChatId = id;
        var conversation = await _chatService.UpdateAsync(serviceUpdateChat);
        return _mapper.Map<ApiChat>(conversation);
    }

    /// <inheritdoc />
    public async Task<ApiChatAvailability> CanStartChat(string username)
    {
        // Get target user to resolve username to ID
        var targetUser = await _userService.Get(username);

        // Check block status between current user and target user
        var blockStatus = await _userBlacklistService.GetBlockStatus(targetUser.UserId);

        return new ApiChatAvailability { CanStart = blockStatus.CanCommunicate };
    }
}
