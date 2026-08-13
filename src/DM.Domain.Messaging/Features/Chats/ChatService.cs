using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Messaging.Authorization;
using FluentValidation;

namespace DM.Domain.Messaging.Features.Chats;

/// <inheritdoc />
internal class ChatService : IChatService
{
    private readonly IValidator<CreateChat> _createValidator;
    private readonly IValidator<UpdateChat> _updateValidator;
    private readonly IChatFactory _factory;
    private readonly IChatRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IGuidFactory _guidFactory;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _userBlacklistChecker;

    public ChatService(
        IValidator<CreateChat> createValidator,
        IValidator<UpdateChat> updateValidator,
        IChatFactory factory,
        IChatRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IIntentionManager intentionManager,
        IGuidFactory guidFactory,
        IIdentityProvider identityProvider,
        IUserBlacklistChecker userBlacklistChecker)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _factory = factory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _intentionManager = intentionManager;
        _guidFactory = guidFactory;
        _identityProvider = identityProvider;
        _userBlacklistChecker = userBlacklistChecker;
    }

    // ═══ CREATE ═══

    /// <inheritdoc />
    public async Task<Chat> CreateGroupAsync(CreateChat createChat)
    {
        await _createValidator.ValidateAndThrowAsync(createChat);

        var currentUserId = _identityProvider.Current.User.UserId;
        var allParticipants = createChat.ParticipantIds
            .Append(currentUserId)
            .Distinct()
            .ToArray();

        await ThrowIfAnyBlocksTheAuthor(allParticipants);

        var (chat, chatLinks) = _factory.CreateGroup(createChat.Title, allParticipants);

        // Markers first, row second, commit on the line after it returns.
        await using var counters = await _unreadCountersRepository.ReserveAsync(
            UnreadMarker.ForReaders(chat.ChatId, UnreadEntryType.Message, allParticipants));

        var result = await _repository.Create(chat, chatLinks);
        counters.Commit();

        return result;
    }

    /// <inheritdoc />
    public async Task<Chat> CreateGameRoomChatAsync(Guid roomId, string title)
    {
        var chat = _factory.CreateGameRoom(roomId, title);
        var result = await _repository.CreateGameRoomChat(chat);
        return result;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public async Task<(IEnumerable<Chat> Chats, PagingResult Paging)> GetAsync(PagingQuery query)
    {
        var identity = _identityProvider.Current;
        var currentUserId = identity.User.UserId;
        var totalCount = await _repository.Count(currentUserId);
        var pagingData = new PagingData(query, identity.Settings.Paging.MessagesPerPage, totalCount);
        var chats = (await _repository.Get(currentUserId, pagingData)).ToArray();
        await _unreadCountersRepository.FillEntityCounters(chats, currentUserId,
            c => c.UnreadEntityId, c => c.UnreadMessagesCount);

        return (chats, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Chat> GetAsync(Guid chatId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var chat = await _repository.Get(chatId, currentUserId);
        if (chat == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
        }

        await _unreadCountersRepository.FillEntityCounters(new[] { chat }, currentUserId,
            c => c.UnreadEntityId, c => c.UnreadMessagesCount);

        return chat;
    }

    /// <inheritdoc />
    public async Task<Chat> GetGameRoomAsync(Guid chatId)
    {
        // Deliberately the unfiltered read: the participation predicate returns
        // null for a game room chat, which has no participant rows by design.
        var chat = await _repository.GetForUpdate(chatId);
        if (chat == null || chat.Type != ChatType.GameRoom)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
        }

        var currentUserId = _identityProvider.Current.User.UserId;
        await _unreadCountersRepository.FillEntityCounters(new[] { chat }, currentUserId,
            c => c.UnreadEntityId, c => c.UnreadMessagesCount);

        return chat;
    }

    /// <inheritdoc />
    public async Task<Chat> GetByPublicIdAsync(string publicId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var chat = await _repository.GetByPublicId(publicId, currentUserId);
        if (chat == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
        }

        await _unreadCountersRepository.FillEntityCounters(new[] { chat }, currentUserId,
            c => c.UnreadEntityId, c => c.UnreadMessagesCount);

        return chat;
    }

    /// <inheritdoc />
    public async Task<Chat> GetOrCreateDirectAsync(string username)
    {
        var otherUserId = await _repository.FindUser(username);
        if (!otherUserId.HasValue)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFound);
        }

        return await GetOrCreateDirectInternalAsync(otherUserId.Value);
    }

    private async Task<Chat> GetOrCreateDirectInternalAsync(Guid otherUserId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var existingChat = await _repository.FindDirectChat(currentUserId, otherUserId);
        if (existingChat != null)
        {
            await _unreadCountersRepository.FillEntityCounters(new[] { existingChat }, currentUserId,
                c => c.UnreadEntityId, c => c.UnreadMessagesCount);
            return existingChat;
        }

        var (chat, chatLinks) = _factory.CreateDirect(currentUserId, otherUserId);

        // Markers first, row second, commit on the line after it returns.
        await using var counters = await _unreadCountersRepository.ReserveAsync(
            UnreadMarker.ForReaders(chat.ChatId, UnreadEntryType.Message,
                new[] { currentUserId, otherUserId }));

        var result = await _repository.Create(chat, chatLinks);
        counters.Commit();

        return result;
    }

    /// <inheritdoc />
    public async Task<int> GetTotalUnreadCountAsync()
    {
        var userId = _identityProvider.Current.User.UserId;
        return (await _unreadCountersRepository.SelectByParentsAsync(userId, UnreadEntryType.Message, userId))[userId];
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid chatId)
    {
        var chat = await _repository.GetForUpdate(chatId);
        if (chat == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
        }

        // Reached through the unfiltered read, so participation is asked here.
        // A conversation is only ever its participants': the endpoint took any
        // identifier somebody remembered, and once removals started leaving a
        // tombstone behind, "mark as read" from a former participant would have
        // put a live marker back — the flush finds no marker of their own, falls
        // through to borrowing a neighbour's parent, and writes a document with
        // no removal stamp, which nothing collects afterwards.
        //
        // Narrowed to the two types that have participants at all. A game room
        // chat and the global chat carry no participant rows by design, and their
        // access is decided before this call.
        if (chat.Type is ChatType.Group or ChatType.Direct &&
            chat.Participants.All(p => p.UserId != _identityProvider.Current.User.UserId))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
        }

        await _unreadCountersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, chat.UnreadEntityId);
    }

    // ═══ UPDATE ═══

    /// <inheritdoc />
    public async Task<Chat> UpdateAsync(UpdateChat updateChat)
    {
        await _updateValidator.ValidateAndThrowAsync(updateChat);

        var chat = await _repository.GetForUpdate(updateChat.ChatId);
        if (chat == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
        }

        _intentionManager.ThrowIfForbidden(ChatIntention.UpdateChat, chat);

        var addParticipants = updateChat.AddParticipants?
            .Except(chat.Participants.Select(p => p.UserId))
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        await ThrowIfAnyBlocksTheAuthor(addParticipants);

        var linksToAdd = addParticipants.Select(userId => new CreateChatLinkEntity
        {
            UserChatLinkId = _guidFactory.Create(),
            ChatId = updateChat.ChatId,
            UserId = userId,
            IsRemoved = false
        });

        var removeParticipants = updateChat.RemoveParticipants?
            .Except(new[] { _identityProvider.Current.User.UserId })
            .ToArray() ?? Array.Empty<Guid>();

        var updateEntity = new UpdateChatEntity
        {
            ChatId = updateChat.ChatId,
            Title = updateChat.Title?.Trim(),
            AddLinks = linksToAdd,
            RemoveUserIds = removeParticipants
        };

        // Markers of everybody joining go in first, the way they do on creation:
        // a participant whose marker never landed borrows the parent of a
        // neighbour when they open the conversation, and a conversation carrying
        // somebody else's parent drops out of its own reader's total.
        var joining = addParticipants.Length > 0
            ? new[] { UnreadMarker.ForReaders(chat.UnreadEntityId, UnreadEntryType.Message, addParticipants) }
            : [];

        await using var counters = await _unreadCountersRepository.ReserveAsync(joining);

        var result = await _repository.Update(updateEntity);
        counters.Commit();

        // The other half of the same lifecycle. Without it a person removed from
        // the chat kept a marker that every later message incremented, on a
        // conversation the participation predicate no longer shows them, and
        // nothing collected it: the expiry index reads the removal stamp, and an
        // untouched marker has none. Coming back repairs itself — the create
        // above replaces the document whole.
        if (removeParticipants.Length > 0)
        {
            await _unreadCountersRepository.DeleteAsync(
                chat.UnreadEntityId,
                UnreadEntryType.Message,
                removeParticipants);
        }

        return result;
    }

    // ═══ BLACKLIST ═══

    /// <summary>
    /// Refuses to put somebody into a group chat with a person who blocked them
    /// </summary>
    /// <remarks>
    /// A group chat used to be the way around a personal blacklist: the message
    /// path asked about BlockDirectMessages for a direct chat alone, so somebody
    /// who had been blocked opened a group with the same person and wrote there
    /// instead. The message path now reads a conversation of two as private
    /// correspondence whatever its type, and this is the other half of that rule.
    /// Neither half closes the hole alone: the pair check is silent while a third
    /// participant is in the room, and that third one can be removed a second
    /// later, while this check cannot see a group that shrinks to a pair after it
    /// was allowed.
    ///
    /// The flag asked about is the same BlockDirectMessages the message path
    /// asks about, and deliberately so: a blacklist entry without it leaves
    /// private messages from that person allowed, so refusing them a shared room
    /// while accepting their letters would be two answers to one question.
    ///
    /// The refusal names nobody and gives no reason. Which of the participants
    /// keeps the author on a blacklist is theirs to know, and the module already
    /// declines to say it elsewhere: the block status answers a general "cannot
    /// communicate" rather than "they blocked you".
    /// </remarks>
    private async Task ThrowIfAnyBlocksTheAuthor(IReadOnlyCollection<Guid> participantIds)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var others = participantIds.Where(id => id != currentUserId).Distinct().ToArray();
        if (others.Length == 0)
        {
            return;
        }

        var blocking = await _userBlacklistChecker.GetOwnersBlockingIfFlagEnabledAsync(
            currentUserId, others, UserBlacklistSettings.BlockDirectMessages);
        if (blocking.Count > 0)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Нельзя добавить в чат этого пользователя");
        }
    }

    // ═══ DELETE ═══

    /// <inheritdoc />
    public async Task DeleteAsync(Guid chatId)
    {
        var chat = await _repository.GetForUpdate(chatId);
        if (chat == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
        }

        _intentionManager.ThrowIfForbidden(ChatIntention.DeleteChat, chat);

        await _repository.Delete(chatId);
        await _unreadCountersRepository.DeleteAsync(chat.UnreadEntityId, UnreadEntryType.Message);
    }
}
