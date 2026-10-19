using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Chats;
using DM.Domain.Core.Content;
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

    /// <inheritdoc />
    public async Task<Message> CreateAsync(CreateMessage createMessage, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createMessage, ct);
        var chat = await _chatService.GetAsync(createMessage.ChatId);
        // MayCreateAsync's first question, so the offered field and this refusal cannot part.
        _intentionManager.ThrowIfForbidden(ChatIntention.CreateMessage, chat);

        return await CreateInternalAsync(chat, createMessage, ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The gates CreateAsync refuses by once the chat is found, in its order and by its
    /// code: the intention, the addressee of a conversation of two, a closed event of the
    /// global chat.
    /// </remarks>
    public async Task<bool> MayCreateAsync(Chat chat, CancellationToken ct = default)
    {
        if (!_intentionManager.IsAllowed(ChatIntention.CreateMessage, chat))
        {
            return false;
        }

        var userId = _identityProvider.Current.User.UserId;
        if (await TheAddresseeBlocksAsync(chat, userId, ct))
        {
            return false;
        }

        return chat.Type != ChatType.Global ||
               !ClosesTheChatTo(await _globalChatEventRepository.GetActiveEvent(), userId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The rule of a conversation of two (TheAddresseeBlocksAsync), asked before the
    /// conversation exists.
    /// </remarks>
    public async Task<bool> MayWriteToAsync(Guid userId, CancellationToken ct = default)
    {
        var writer = _identityProvider.Current.User;
        return writer.IsAuthenticated &&
               writer.UserId != userId &&
               !await BlocksTheWriterAsync(userId, writer.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<Message> CreateInGameRoomAsync(CreateMessage createMessage, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createMessage, ct);
        // No participation check: the game module has decided access to the room, and the
        // read accepts nothing but a game room chat.
        var chat = await _chatService.GetGameRoomAsync(createMessage.ChatId);

        return await CreateInternalAsync(chat, createMessage, ct);
    }

    private async Task<Message> CreateInternalAsync(
        Chat chat, CreateMessage createMessage, CancellationToken ct)
    {
        var userId = _identityProvider.Current.User.UserId;

        // A group left with two participants is private correspondence as a direct chat
        // is; ChatService refuses to put a blocked writer into a group at all.
        if (await TheAddresseeBlocksAsync(chat, userId, ct))
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя отправить сообщение этому пользователю");
        }

        var activeEvent = chat.Type == ChatType.Global
            ? await _globalChatEventRepository.GetActiveEvent()
            : null;
        if (ClosesTheChatTo(activeEvent, userId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Идет закрытый эвент. Писать могут только его участники.");
        }

        createMessage.Text = AsWritten(createMessage.Text, chat.Type);

        var message = _factory.Create(createMessage, userId);

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
            chat.UnreadEntityId, UnreadEntryType.Message, userId);
        await _producer.SendAsync(EventType.NewMessage, message.MessageId);
        if (chat.Type == ChatType.Global)
        {
            // The global chat is public: realtime consumers broadcast it to every client.
            await _producer.SendAsync(EventType.NewGlobalChatMessage, message.MessageId);
        }

        return result;
    }

    /// <summary>
    /// Whether the chat is a conversation of two whose other participant closed
    /// private messages to the writer. A direct chat is always that; a group is
    /// that once it is left with two participants.
    /// </summary>
    private async Task<bool> TheAddresseeBlocksAsync(Chat chat, Guid writerId, CancellationToken ct)
    {
        if (chat.Type is not (ChatType.Direct or ChatType.Group))
        {
            return false;
        }

        var addressees = chat.Participants.Where(p => p.UserId != writerId).ToArray();
        return addressees.Length == 1 && await BlocksTheWriterAsync(addressees[0].UserId, writerId, ct);
    }

    /// <summary>
    /// Whether the addressee keeps the writer on his blacklist with private
    /// messages blocked. An entry without the switch leaves them open.
    /// </summary>
    private async Task<bool> BlocksTheWriterAsync(Guid addresseeId, Guid writerId, CancellationToken ct)
    {
        var blockedIds = await _userBlacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(
            addresseeId, UserBlacklistSettings.BlockDirectMessages, ct);
        return blockedIds.Contains(writerId);
    }

    /// <summary>
    /// Whether the active event of the global chat is a closed one the writer
    /// does not take part in: only its participants write while it goes on.
    /// </summary>
    private static bool ClosesTheChatTo(GlobalChatEvent? activeEvent, Guid writerId) =>
        activeEvent is { IsOpen: false } &&
        !activeEvent.Participants.Any(p => p.User.UserId == writerId);

    /// <inheritdoc />
    public async Task<Message> GetAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _repository.Get(messageId, _identityProvider.Current.User.UserId, ct);
        if (message == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.MessageNotFound);
        }

        return message;
    }

    /// <inheritdoc />
    public async Task<Message> GetGlobalChatMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _repository.GetGlobalChatMessage(messageId, ct);
        if (message == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.MessageNotFound);
        }

        return message;
    }

    /// <inheritdoc />
    public async Task<CursorResult<Message>> GetWithCursorAsync(
        Guid chatId, CursorQuery query, CancellationToken ct = default)
    {
        var chat = await _chatService.GetAsync(chatId);

        // What the reader cleared for himself is not given back to him on any page.
        query.HistoryClearedUtc = chat.HistoryClearedUtc;

        // The hidden authors are the general chat's alone: a private conversation with a
        // blocked person is closed by BlockDirectMessages instead, and a hidden member of a
        // group would leave the rest answering nobody. The live feed needs no filter of its
        // own: a push only asks the page to fetch the rest through this method.
        var user = _identityProvider.Current.User;
        if (chatId == WellKnownChats.GlobalChatId && user.IsAuthenticated)
        {
            query.HiddenAuthorIds = await _userBlacklistChecker.HiddenAuthorsAsync(user.UserId, ct);
        }

        return await _repository.GetWithCursor(chatId, query, ct);
    }

    /// <inheritdoc />
    public async Task<CursorResult<Message>> GetGameRoomWithCursorAsync(
        Guid chatId, CursorQuery query, CancellationToken ct = default)
    {
        // The game module checked the room; this only confirms a game room chat.
        await _chatService.GetGameRoomAsync(chatId);

        return await _repository.GetWithCursor(chatId, query, ct);
    }

    /// <inheritdoc />
    public async Task<Message> UpdateAsync(UpdateMessage updateMessage)
    {
        await _updateValidator.ValidateAndThrowAsync(updateMessage);
        return await UpdateInternalAsync(updateMessage, await GetAsync(updateMessage.MessageId));
    }

    /// <inheritdoc />
    public async Task<Message> UpdateGlobalChatMessageAsync(UpdateMessage updateMessage)
    {
        await _updateValidator.ValidateAndThrowAsync(updateMessage);
        return await UpdateInternalAsync(
            updateMessage, await GetGlobalChatMessageAsync(updateMessage.MessageId));
    }

    /// <summary>
    /// Whether a message of the chat is read with [mod]: global and game-room
    /// chats draw it, direct and group chats do not.
    /// </summary>
    private static bool DrawsModBlock(ChatType chatType) => chatType is ChatType.Global or ChatType.GameRoom;

    /// <summary>
    /// The text as the chat stores it: without the [mod] of a non-moderator where the
    /// tag renders.
    /// </summary>
    [return: NotNullIfNotNull(nameof(text))]
    private string? AsWritten(string? text, ChatType chatType) =>
        DrawsModBlock(chatType) && !string.IsNullOrEmpty(text)
            ? ModBlockSanitizer.SanitizeForAuthor(text, _identityProvider.Current.User.Role)
            : text;

    private async Task<Message> UpdateInternalAsync(UpdateMessage updateMessage, Message message)
    {
        _intentionManager.ThrowIfForbidden(MessageIntention.Edit, message);

        var updateEntity = new UpdateMessageEntity
        {
            MessageId = updateMessage.MessageId,
            Text = AsWritten(AuthoredText.AsTyped(updateMessage.Text), message.ChatType),
            EditorUserId = _identityProvider.Current.User.UserId
        };

        var updatedMessage = await _repository.Update(updateEntity);
        await _producer.SendAsync(EventType.ChangedMessage, updatedMessage.Id);
        return updatedMessage;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid messageId) =>
        await DeleteInternalAsync(await GetAsync(messageId));

    /// <inheritdoc />
    public async Task DeleteGlobalChatMessageAsync(Guid messageId) =>
        await DeleteInternalAsync(await GetGlobalChatMessageAsync(messageId));

    private async Task DeleteInternalAsync(Message message)
    {
        var currentUserId = _identityProvider.Current.User.UserId;

        _intentionManager.ThrowIfForbidden(MessageIntention.Delete, message);

        await _repository.Delete(message.Id, currentUserId);

        // The counter comes down with the message, under the chat's counter identifier.
        // A game room chat has no participant rows, so it is read as the create path reads it.
        var chat = message.ChatType == ChatType.GameRoom
            ? await _chatService.GetGameRoomAsync(message.ChatId)
            : await _chatService.GetAsync(message.ChatId);
        await _unreadCountersRepository.DecrementAsync(
            chat.UnreadEntityId, UnreadEntryType.Message, message.CreatedUtc);

        await _producer.SendAsync(EventType.DeletedMessage, message.Id);
    }
}
