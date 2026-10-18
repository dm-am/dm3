using System;
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
    private readonly IBbTagNameCase _tagNames;

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
        IUserBlacklistChecker userBlacklistChecker,
        IBbTagNameCase tagNames)
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
        _tagNames = tagNames;
    }

    // ═══ CREATE ═══

    /// <inheritdoc />
    public async Task<Message> CreateAsync(CreateMessage createMessage, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createMessage, ct);
        var chat = await _chatService.GetAsync(createMessage.ChatId);
        // MayCreateAsync's first question, so the field the page offers and this
        // refusal cannot part.
        _intentionManager.ThrowIfForbidden(ChatIntention.CreateMessage, chat);

        return await CreateInternalAsync(chat, createMessage, ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The three gates CreateAsync is refused by once the chat is found, asked in
    /// the same order and by the same code: the intention (a participant of a
    /// conversation, a signed-in reader under no ban in the global chat), the one
    /// addressee of a conversation of two who closed private messages to the
    /// writer, and a closed event of the global chat.
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
    /// The rule a conversation of two answers to (TheAddresseeBlocksAsync),
    /// asked before the conversation exists: the profile leads to it, and opening
    /// the conversation does not ask it, the first message does.
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
        // No participation check here on purpose: a game room chat has no
        // participant rows, so the rule applied above refuses everyone, the
        // master included. Access to a game room chat is access to its room, and
        // the game module has decided that before this call. The read below
        // accepts nothing but a game room chat, so this path cannot reach private
        // correspondence.
        var chat = await _chatService.GetGameRoomAsync(createMessage.ChatId);

        return await CreateInternalAsync(chat, createMessage, ct);
    }

    private async Task<Message> CreateInternalAsync(
        Chat chat, CreateMessage createMessage, CancellationToken ct)
    {
        var userId = _identityProvider.Current.User.UserId;

        // Private correspondence is a conversation of exactly two people, and its
        // one addressee answers for it with BlockDirectMessages. A direct chat is
        // always that; a group left with two participants is the same conversation
        // wearing another type, and asking about the type alone left the setting
        // one "create a group" away from being worked around.
        //
        // The other half of the rule stands at the door in ChatService, which
        // refuses to put somebody into a group with a person who blocked them.
        // Both are needed: the door cannot see a group that shrinks to a pair
        // afterwards, and this cannot see a group of three.
        //
        // MayCreateAsync and MayWriteToAsync ask the same two helpers, so the
        // field and the button the pages offer and this refusal cannot part.
        if (await TheAddresseeBlocksAsync(chat, userId, ct))
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя отправить сообщение этому пользователю");
        }

        // For global chat, check if there's an active event with restrictions
        var activeEvent = chat.Type == ChatType.Global
            ? await _globalChatEventRepository.GetActiveEvent()
            : null;
        if (ClosesTheChatTo(activeEvent, userId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Идет закрытый эвент. Писать могут только его участники.");
        }

        // Global and game-room messages render on surfaces where [mod] is a
        // green mod block; direct/group messages do not. Strip [mod] authored
        // by a non-moderator in the former; Moderator+ may author it.
        if (chat.Type is ChatType.Global or ChatType.GameRoom)
            createMessage.Text = ModBlockSanitizer.SanitizeForAuthor(
                createMessage.Text, _identityProvider.Current.User.Role);

        // The names of the tags in lower case, as the site writes them; [mod]
        // is a tag only where it renders.
        createMessage.Text = _tagNames.ToLowerCase(createMessage.Text, TagsOf(chat.Type));

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
            chat.UnreadEntityId, UnreadEntryType.Message, userId);
        await _producer.SendAsync(EventType.NewMessage, message.MessageId);
        if (chat.Type == ChatType.Global)
        {
            // Global chat is public: a dedicated event lets realtime
            // consumers broadcast it to all connected clients
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
        !(activeEvent.Participants?.Any(p => p.User.UserId == writerId) ?? false);

    // ═══ READ ═══

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
        // Validate chat access (throws if user doesn't have access)
        var chat = await _chatService.GetAsync(chatId);

        // What the reader cleared for himself is not given back to him, on any
        // page and by any of the ways of reaching one: the latest, a cursor, a
        // message to go to or a date.
        query.HistoryClearedUtc = chat.HistoryClearedUtc;

        // "Скрывать сообщения в чате" is about the general chat, the one room
        // everybody shares: a private conversation with a blocked person is
        // closed by BlockDirectMessages instead, and hiding one participant of a
        // group would leave the rest answering nobody. The live feed reads the
        // same way - a push only tells the page to fetch what came after its
        // last message, through this method - so a hidden author does not
        // appear between two reloads either.
        var user = _identityProvider.Current.User;
        if (chatId == WellKnownChats.GlobalChatId && user.IsAuthenticated)
        {
            var hidden = await _userBlacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(
                user.UserId, UserBlacklistSettings.HideMessages, ct);
            query.HiddenAuthorIds = hidden.Count > 0 ? hidden : null;
        }

        return await _repository.GetWithCursor(chatId, query, ct);
    }

    /// <inheritdoc />
    public async Task<CursorResult<Message>> GetGameRoomWithCursorAsync(
        Guid chatId, CursorQuery query, CancellationToken ct = default)
    {
        // The room check happened in the game module. What is confirmed here is
        // only that the chat really is a game room one, so this reader cannot be
        // pointed at a direct or group chat.
        await _chatService.GetGameRoomAsync(chatId);

        return await _repository.GetWithCursor(chatId, query, ct);
    }

    // ═══ UPDATE ═══

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
    /// The tags a message of the chat is read with: global and game-room chats
    /// draw [mod], direct and group chats do not.
    /// </summary>
    private static BbTagSet TagsOf(ChatType chatType) =>
        chatType is ChatType.Global or ChatType.GameRoom ? BbTagSet.WithModBlock : BbTagSet.Common;

    private async Task<Message> UpdateInternalAsync(UpdateMessage updateMessage, Message message)
    {
        _intentionManager.ThrowIfForbidden(MessageIntention.Edit, message);

        var text = updateMessage.Text?.Trim();
        // Strip [mod] authored by a non-moderator when editing a message on a
        // surface that renders it (global / game-room chat); Moderator+ may
        // author it. Direct/group edits keep [mod] as literal text.
        if (!string.IsNullOrEmpty(text) && message.ChatType is ChatType.Global or ChatType.GameRoom)
            text = ModBlockSanitizer.SanitizeForAuthor(text, _identityProvider.Current.User.Role);

        text = _tagNames.ToLowerCase(text, TagsOf(message.ChatType));

        var updateEntity = new UpdateMessageEntity
        {
            MessageId = updateMessage.MessageId,
            Text = text,
            EditorUserId = _identityProvider.Current.User.UserId
        };

        var updatedMessage = await _repository.Update(updateEntity);
        await _producer.SendAsync(EventType.ChangedMessage, updatedMessage.Id);
        return updatedMessage;
    }

    // ═══ DELETE ═══

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

        // The counter comes down with the message, the way it does for every
        // other kind of comment on the site — blog, publication, topic, game,
        // post and character all decrement here. Without it the badge kept
        // counting a message that no longer exists, until something happened to
        // flush the whole conversation.
        //
        // Addressed by the chat's counter identifier rather than by ChatId: for a
        // game room chat those are different things, and using the chat's own is
        // how the room's unread went dead in the first place.
        //
        // Read the same way the create path reads it. A game room chat has no
        // participant rows, so the participation-filtered read refuses everyone
        // for it — including the author of the message being deleted.
        var chat = message.ChatType == ChatType.GameRoom
            ? await _chatService.GetGameRoomAsync(message.ChatId)
            : await _chatService.GetAsync(message.ChatId);
        await _unreadCountersRepository.DecrementAsync(
            chat.UnreadEntityId, UnreadEntryType.Message, message.CreatedUtc);

        await _producer.SendAsync(EventType.DeletedMessage, message.Id);
    }
}
