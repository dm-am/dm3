using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
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

    public ChatService(
        IValidator<CreateChat> createValidator,
        IValidator<UpdateChat> updateValidator,
        IChatFactory factory,
        IChatRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IIntentionManager intentionManager,
        IGuidFactory guidFactory,
        IIdentityProvider identityProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _factory = factory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _intentionManager = intentionManager;
        _guidFactory = guidFactory;
        _identityProvider = identityProvider;
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

        var (chat, chatLinks) = _factory.CreateGroup(createChat.Title, allParticipants);
        var result = await _repository.Create(chat, chatLinks);

        await _unreadCountersRepository.CreateAsync(result.Id, UnreadEntryType.Message, allParticipants);

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
        var result = await _repository.Create(chat, chatLinks);

        await _unreadCountersRepository.CreateAsync(result.Id, UnreadEntryType.Message,
            new[] { currentUserId, otherUserId }.Distinct());

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

        var result = await _repository.Update(updateEntity);

        if (addParticipants.Length > 0)
        {
            await _unreadCountersRepository.CreateAsync(
                updateChat.ChatId,
                UnreadEntryType.Message,
                addParticipants);
        }

        return result;
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
