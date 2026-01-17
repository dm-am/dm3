using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Common.Extensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;

namespace DM.Services.Community.BusinessProcesses.Messaging.Reading;

/// <inheritdoc />
internal class ConversationReadingService : IConversationReadingService
{
    private readonly IConversationFactory _factory;
    private readonly IConversationReadingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public ConversationReadingService(
        IConversationFactory factory,
        IConversationReadingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider)
    {
        _factory = factory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Conversation> conversations, PagingResult paging)> Get(PagingQuery query)
    {
        var identity = _identityProvider.Current;
        var currentUserId = identity.User.UserId;
        var totalCount = await _repository.Count(currentUserId);
        var pagingData = new PagingData(query, identity.Settings.Paging.MessagesPerPage, totalCount);
        var conversations = (await _repository.Get(currentUserId, pagingData)).ToArray();
        await _unreadCountersRepository.FillEntityCounters(conversations, currentUserId,
            c => c.Id, c => c.UnreadMessagesCount);

        return (conversations, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Conversation> Get(Guid conversationId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var conversation = await _repository.Get(conversationId, currentUserId);
        if (conversation == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Conversation not found");
        }

        await _unreadCountersRepository.FillEntityCounters(new[] {conversation}, currentUserId,
            c => c.Id, c => c.UnreadMessagesCount);

        return conversation;
    }

    /// <inheritdoc />
    public async Task<Conversation> GetOrCreate(string login)
    {
        var visaviId = await _repository.FindUser(login);
        if (!visaviId.HasValue)
        {
            throw new HttpException(HttpStatusCode.Gone, "User not found");
        }

        var currentUserId = _identityProvider.Current.User.UserId;
        var existingConversation = await _repository.FindVisaviConversation(currentUserId, visaviId.Value);
        if (existingConversation != null)
        {
            await _unreadCountersRepository.FillEntityCounters(new[] {existingConversation}, currentUserId,
                c => c.Id, c => c.UnreadMessagesCount);
            return existingConversation;
        }

        var (conversation, conversationLinks) = _factory.CreateVisavi(currentUserId, visaviId.Value);
        var result = await _repository.Create(conversation, conversationLinks);

        await _unreadCountersRepository.Create(result.Id, UnreadEntryType.Message,
            new[] {currentUserId, visaviId.Value}.Distinct());

        return result;
    }

    /// <inheritdoc />
    public async Task<int> GetTotalUnreadCount()
    {
        var userId = _identityProvider.Current.User.UserId;
        return (await _unreadCountersRepository.SelectByParents(userId, UnreadEntryType.Message, userId))[userId];
    }

    /// <inheritdoc />
    public Task MarkAsRead(Guid conversationId) =>
        _unreadCountersRepository.Flush(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, conversationId);
}