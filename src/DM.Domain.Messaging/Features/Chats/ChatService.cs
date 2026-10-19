using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
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
    private readonly IUserReadRepository _userRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ChatService(
        IValidator<CreateChat> createValidator,
        IValidator<UpdateChat> updateValidator,
        IChatFactory factory,
        IChatRepository repository,
        IUserReadRepository userRepository,
        IUnreadCountersRepository unreadCountersRepository,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IUserBlacklistChecker userBlacklistChecker,
        IDateTimeProvider dateTimeProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _factory = factory;
        _repository = repository;
        _userRepository = userRepository;
        _unreadCountersRepository = unreadCountersRepository;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _userBlacklistChecker = userBlacklistChecker;
        _dateTimeProvider = dateTimeProvider;
    }

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
        return await CreateWithMarkers(chat, chatLinks, allParticipants);
    }

    /// <summary>
    /// Markers first, row second, commit on the line after it returns. The first
    /// reservation of the file, which UnreadMarkerOwnershipShould reads the order by.
    /// </summary>
    private async Task<Chat> CreateWithMarkers(
        CreateChatEntity chat, IEnumerable<CreateChatLinkEntity> chatLinks, IEnumerable<Guid> readers)
    {
        await using var counters = await _unreadCountersRepository.ReserveAsync(
            UnreadMarker.ForReaders(chat.ChatId, UnreadEntryType.Message, readers));

        var result = await _repository.Create(chat, chatLinks);
        counters.Commit();

        return result;
    }

    /// <inheritdoc />
    public async Task<Chat> CreateGameRoomChatAsync(Guid roomId, string title) =>
        await _repository.CreateGameRoomChat(_factory.CreateGameRoom(roomId, title));

    /// <inheritdoc />
    public async Task<(IEnumerable<Chat> Chats, PagingResult Paging)> GetAsync(PagingQuery query)
    {
        var identity = _identityProvider.Current;
        var currentUserId = identity.User.UserId;
        var totalCount = await _repository.Count(currentUserId);
        var pagingData = new PagingData(query, identity.Settings.PageSize, totalCount);
        var chats = (await _repository.Get(currentUserId, pagingData)).ToArray();
        await _unreadCountersRepository.FillEntityCounters(chats, currentUserId,
            c => c.UnreadEntityId, c => c.UnreadMessagesCount);

        return (chats, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Chat> GetAsync(Guid chatId)
    {
        var chat = await GetForCurrentUserAsync(chatId);
        await FillUnreadCounters(chat, _identityProvider.Current.User.UserId);

        return chat;
    }

    /// <inheritdoc />
    public async Task<Chat> GetGameRoomAsync(Guid chatId)
    {
        // The unfiltered read: a game room chat has no participant rows.
        var chat = await _repository.GetForUpdate(chatId);
        if (chat == null || chat.Type != ChatType.GameRoom)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
        }

        var currentUserId = _identityProvider.Current.User.UserId;
        await FillUnreadCounters(chat, currentUserId);

        return chat;
    }

    /// <inheritdoc />
    public async Task<Chat> GetByPublicIdAsync(string publicId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var chat = OrNotFound(await _repository.GetByPublicId(publicId, currentUserId));
        await FillUnreadCounters(chat, currentUserId);

        return chat;
    }

    /// <inheritdoc />
    public async Task<Chat> GetOrCreateDirectAsync(string username)
    {
        var otherUserId = await _userRepository.FindUserIdAsync(username);
        if (!otherUserId.HasValue)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFound);
        }

        var currentUserId = _identityProvider.Current.User.UserId;
        var existingChat = await _repository.FindDirectChat(currentUserId, otherUserId.Value);
        if (existingChat != null)
        {
            await FillUnreadCounters(existingChat, currentUserId);
            return existingChat;
        }

        var (chat, chatLinks) = _factory.CreateDirect(currentUserId, otherUserId.Value);
        return await CreateWithMarkers(chat, chatLinks, [currentUserId, otherUserId.Value]);
    }

    /// <inheritdoc />
    public async Task<long> CountUnreadAsync()
    {
        var currentUserId = _identityProvider.Current.User.UserId;

        // A conversation has no container, so its reader is the parent of its marker.
        var totals = await _unreadCountersRepository.SelectTotalUnreadByParentsAsync(
            currentUserId, UnreadEntryType.Message, currentUserId);

        return totals[currentUserId];
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid chatId)
    {
        var chat = OrNotFound(await _repository.GetForUpdate(chatId));

        // The read is unfiltered, and a former participant must not put his marker back.
        // A game room chat and the global chat have no participant rows and are decided before.
        if (chat.Type is ChatType.Group or ChatType.Direct &&
            chat.Participants.All(p => p.UserId != _identityProvider.Current.User.UserId))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
        }

        await _unreadCountersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, chat.UnreadEntityId);
    }

    /// <inheritdoc />
    public async Task<Chat> UpdateAsync(UpdateChat updateChat)
    {
        await _updateValidator.ValidateAndThrowAsync(updateChat);

        var chat = OrNotFound(await _repository.GetForUpdate(updateChat.ChatId));
        _intentionManager.ThrowIfForbidden(ChatIntention.UpdateChat, chat);

        var addParticipants = updateChat.AddParticipants?
            .Except(chat.Participants.Select(p => p.UserId))
            .ToArray() ?? Array.Empty<Guid>();

        await ThrowIfAnyBlocksTheAuthor(addParticipants);

        var linksToAdd = _factory.CreateLinks(updateChat.ChatId, addParticipants);

        var removeParticipants = updateChat.RemoveParticipants?
            .Except(new[] { _identityProvider.Current.User.UserId })
            .ToArray() ?? Array.Empty<Guid>();

        var updateEntity = new UpdateChatEntity
        {
            ChatId = updateChat.ChatId,
            Title = AuthoredText.AsTyped(updateChat.Title),
            AddLinks = linksToAdd,
            RemoveUserIds = removeParticipants
        };

        // Markers of everybody joining go in first, as on creation: a missing marker
        // borrows a neighbour's parent and drops out of its reader's total.
        var joining = addParticipants.Length > 0
            ? new[] { UnreadMarker.ForReaders(chat.UnreadEntityId, UnreadEntryType.Message, addParticipants) }
            : [];

        await using var counters = await _unreadCountersRepository.ReserveAsync(joining);

        var result = await _repository.Update(updateEntity, _identityProvider.Current.User.UserId);
        counters.Commit();

        // Left behind, the marker of a removed participant counts every later message.
        if (removeParticipants.Length > 0)
        {
            await _unreadCountersRepository.DeleteAsync(
                chat.UnreadEntityId,
                UnreadEntryType.Message,
                removeParticipants);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task LeaveAsync(Guid chatId)
    {
        var chat = await GetForCurrentUserAsync(chatId);
        _intentionManager.ThrowIfForbidden(ChatIntention.LeaveChat, chat);

        var userId = _identityProvider.Current.User.UserId;
        await _repository.Leave(chat.Id, userId, _dateTimeProvider.Now);

        // The marker goes with the link, as it does on a removal (UpdateAsync).
        await _unreadCountersRepository.DeleteAsync(
            chat.UnreadEntityId, UnreadEntryType.Message, new[] { userId });
    }

    /// <inheritdoc />
    public async Task ClearHistoryAsync(Guid chatId)
    {
        var chat = await GetForCurrentUserAsync(chatId);
        _intentionManager.ThrowIfForbidden(ChatIntention.ClearHistory, chat);

        var userId = _identityProvider.Current.User.UserId;
        await _repository.ClearHistory(chat.Id, userId, _dateTimeProvider.Now);

        // Whatever was unread is out of his reach now.
        await _unreadCountersRepository.FlushAsync(userId, UnreadEntryType.Message, chat.UnreadEntityId);
    }

    /// <inheritdoc />
    public bool MayLeave(Chat chat) =>
        _intentionManager.IsAllowed(ChatIntention.LeaveChat, chat);

    /// <inheritdoc />
    public bool MayClearHistory(Chat chat) =>
        _intentionManager.IsAllowed(ChatIntention.ClearHistory, chat);

    /// <summary>
    /// The chat as the current user reads it: a conversation of other people is not
    /// found here, as on its every read, rather than refused.
    /// </summary>
    private async Task<Chat> GetForCurrentUserAsync(Guid chatId) =>
        OrNotFound(await _repository.Get(chatId, _identityProvider.Current.User.UserId));

    private static Chat OrNotFound(Chat? chat) =>
        chat ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);

    /// <summary>
    /// Refuses to put somebody into a group chat with a person who blocked his private
    /// messages: a group is not a way around that setting. The message path closes the
    /// other half, a group that shrinks to a pair. The flag is the same BlockDirectMessages,
    /// or a shared room and letters would answer one question differently. The refusal
    /// names nobody: who keeps whom on a blacklist is theirs to know.
    /// </summary>
    private async Task ThrowIfAnyBlocksTheAuthor(IReadOnlyCollection<Guid> participantIds)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var others = participantIds.Where(id => id != currentUserId).ToArray();
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

    /// <inheritdoc />
    public async Task DeleteAsync(Guid chatId)
    {
        var chat = OrNotFound(await _repository.GetForUpdate(chatId));
        _intentionManager.ThrowIfForbidden(ChatIntention.DeleteChat, chat);

        await _repository.Delete(chatId);
        await _unreadCountersRepository.DeleteAsync(chat.UnreadEntityId, UnreadEntryType.Message);
    }

    private Task FillUnreadCounters(Chat chat, Guid currentUserId) =>
        _unreadCountersRepository.FillEntityCounters(new[] { chat }, currentUserId,
            c => c.UnreadEntityId, c => c.UnreadMessagesCount);
}
