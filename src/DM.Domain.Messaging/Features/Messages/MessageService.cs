using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Messaging.Features.Messages;

/// <inheritdoc />
internal class MessageService : IMessageService
{
    private readonly IChatService _chatService;
    private readonly IGlobalChatEventRepository _globalChatEventRepository;
    private readonly IValidator<CreateMessage> _createValidator;
    private readonly IValidator<UpdateMessage> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IMessageFactory _factory;
    private readonly IMessageRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _userBlacklistChecker;

    public MessageService(
        IChatService chatService,
        IGlobalChatEventRepository globalChatEventRepository,
        IValidator<CreateMessage> createValidator,
        IValidator<UpdateMessage> updateValidator,
        IIntentionManager intentionManager,
        IMessageFactory factory,
        IMessageRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IEventProducer producer,
        IIdentityProvider identityProvider,
        IUserBlacklistChecker userBlacklistChecker)
    {
        _chatService = chatService;
        _globalChatEventRepository = globalChatEventRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _factory = factory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
        _identityProvider = identityProvider;
        _userBlacklistChecker = userBlacklistChecker;
    }

    // ═══ CREATE ═══

    /// <inheritdoc />
    public async Task<Message> CreateAsync(CreateMessage createMessage, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createMessage, ct);
        var chat = await _chatService.GetAsync(createMessage.ChatId);
        _intentionManager.ThrowIfForbidden(ChatIntention.CreateMessage, chat);

        var userId = _identityProvider.Current.User.UserId;

        // For direct chats, check if recipient has blocked sender with BlockDirectMessages enabled
        if (chat.Type == ChatType.Direct)
        {
            var otherUser = chat.Participants.FirstOrDefault(p => p.UserId != userId);
            if (otherUser != null)
            {
                // Check if recipient has BlockDirectMessages enabled AND has blocked sender
                var blockedIds = await _userBlacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(
                    otherUser.UserId, UserBlacklistSettings.BlockDirectMessages, ct);
                if (blockedIds.Contains(userId))
                {
                    throw new HttpException(HttpStatusCode.Forbidden, "Cannot send message to this user");
                }
            }
        }

        GlobalChatEvent? activeEvent = null;

        // For global chat, check if there's an active event with restrictions
        if (chat.Type == ChatType.Global)
        {
            activeEvent = await _globalChatEventRepository.GetActiveEvent();
            if (activeEvent != null && !activeEvent.IsOpen)
            {
                // Closed event - only participants can send messages
                var isParticipant = activeEvent.Participants?.Any(p => p.User.UserId == userId) ?? false;
                if (!isParticipant)
                {
                    throw new HttpException(HttpStatusCode.Forbidden,
                        "There is a closed chat event in progress. Only event participants can send messages.");
                }
            }
        }

        var message = _factory.Create(createMessage, userId);

        // If there's an active event, link the message to it
        if (activeEvent != null)
        {
            message.GlobalChatEventId = activeEvent.Id;
        }

        var updateChat = new UpdateChatLastMessageEntity
        {
            ChatId = chat.Id,
            LastMessageId = message.MessageId
        };

        var result = await _repository.Create(message, updateChat, ct);
        await _unreadCountersRepository.IncrementExcludingAsync(
            chat.Id, UnreadEntryType.Message, userId);
        await _producer.SendAsync(EventType.NewMessage, message.MessageId);

        return result;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public async Task<Message> GetAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _repository.Get(messageId, _identityProvider.Current.User.UserId, ct);
        if (message == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Message not found");
        }

        return message;
    }

    /// <inheritdoc />
    public async Task<CursorResult<Message>> GetWithCursorAsync(
        Guid chatId, CursorQuery query, CancellationToken ct = default)
    {
        // Validate chat access (throws if user doesn't have access)
        await _chatService.GetAsync(chatId);

        return await _repository.GetWithCursor(chatId, query, ct);
    }

    // ═══ UPDATE ═══

    /// <inheritdoc />
    public async Task<Message> UpdateAsync(UpdateMessage updateMessage)
    {
        await _updateValidator.ValidateAndThrowAsync(updateMessage);
        var message = await GetAsync(updateMessage.MessageId);

        _intentionManager.ThrowIfForbidden(MessageIntention.Edit, message);

        var updateEntity = new UpdateMessageEntity
        {
            MessageId = updateMessage.MessageId,
            Text = updateMessage.Text?.Trim()
        };

        var updatedMessage = await _repository.Update(updateEntity);
        await _producer.SendAsync(EventType.ChangedMessage, updatedMessage.Id);
        return updatedMessage;
    }

    // ═══ DELETE ═══

    /// <inheritdoc />
    public async Task DeleteAsync(Guid messageId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var message = await _repository.Get(messageId, currentUserId);
        if (message == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Message not found");
        }

        _intentionManager.ThrowIfForbidden(MessageIntention.Delete, message);

        await _repository.Delete(messageId, currentUserId);
        await _producer.SendAsync(EventType.DeletedMessage, messageId);
    }
}
