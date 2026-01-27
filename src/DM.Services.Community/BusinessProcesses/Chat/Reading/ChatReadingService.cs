using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;

namespace DM.Services.Community.BusinessProcesses.Chat.Reading;

/// <inheritdoc />
internal class ChatReadingService : IChatReadingService
{
    private readonly IChatReadingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public ChatReadingService(
        IChatReadingRepository repository,
        IIdentityProvider identityProvider)
    {
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<ChatMessage> messages, PagingResult paging)> GetMessages(PagingQuery pagingQuery)
    {
        var totalCount = await _repository.Count();
        var pageSize = _identityProvider.Current.Settings.Paging.EntitiesPerPage;
        var pagingData = new PagingData(pagingQuery, pageSize, totalCount);
        var chatMessages = await _repository.Get(pagingData);

        return (chatMessages, pagingData.Result);
    }

    /// <inheritdoc />
    public Task<IEnumerable<ChatMessage>> GetNewMessages(DateTimeOffset since) => _repository.Get(since);

    /// <inheritdoc />
    public async Task<ChatMessage> GetMessage(Guid id)
    {
        var chatMessage = await _repository.Get(id);
        if (chatMessage == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Message not found");
        }

        return chatMessage;
    }

    /// <inheritdoc />
    public Task<IEnumerable<ChatMessage>> GetMessagesByDate(DateOnly date) => _repository.GetByDate(date);

    /// <inheritdoc />
    public async Task<(IEnumerable<ChatMessage> messages, bool hasMoreBefore)> GetMessagesBefore(Guid messageId, int count)
    {
        var messages = await _repository.GetBefore(messageId, count);
        var messagesList = messages.ToList();
        var hasMoreBefore = messagesList.Count > 0 && await _repository.HasMessagesBefore(messagesList.First().Id);
        return (messagesList, hasMoreBefore);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<ChatMessage> messages, bool hasMoreAfter)> GetMessagesAfter(Guid messageId, int count)
    {
        var messages = await _repository.GetAfter(messageId, count);
        var messagesList = messages.ToList();
        var hasMoreAfter = messagesList.Count > 0 && await _repository.HasMessagesAfter(messagesList.Last().Id);
        return (messagesList, hasMoreAfter);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<ChatMessage> messages, bool hasMoreBefore, bool hasMoreAfter)> GetMessagesAround(Guid messageId, int count)
    {
        var messages = await _repository.GetAround(messageId, count);
        var messagesList = messages.ToList();

        if (messagesList.Count == 0)
        {
            return (messagesList, false, false);
        }

        var hasMoreBefore = await _repository.HasMessagesBefore(messagesList.First().Id);
        var hasMoreAfter = await _repository.HasMessagesAfter(messagesList.Last().Id);

        return (messagesList, hasMoreBefore, hasMoreAfter);
    }

    /// <inheritdoc />
    public Task<ChatMessage> GetFirstMessageOnOrAfterDate(DateOnly date) =>
        _repository.GetFirstOnOrAfterDate(date);

    /// <inheritdoc />
    public Task<ChatMessage> GetLastMessageOnOrBeforeDate(DateOnly date) =>
        _repository.GetLastOnOrBeforeDate(date);
}