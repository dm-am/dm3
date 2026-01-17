using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;

namespace DM.Services.Community.BusinessProcesses.Messaging.Reading;

/// <inheritdoc />
internal class MessageReadingService : IMessageReadingService
{
    private readonly IConversationReadingService _conversationReadingService;
    private readonly IMessageReadingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public MessageReadingService(
        IConversationReadingService conversationReadingService,
        IMessageReadingRepository repository,
        IIdentityProvider identityProvider)
    {
        _conversationReadingService = conversationReadingService;
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Message> messages, PagingResult paging)> Get(
        Guid conversationId, PagingQuery query, CancellationToken ct = default)
    {
        await _conversationReadingService.Get(conversationId);
        var totalCount = await _repository.Count(conversationId, ct);
        var pagingData = new PagingData(query,
            _identityProvider.Current.Settings.Paging.MessagesPerPage, totalCount);
        var messages = await _repository.Get(conversationId, pagingData, ct);
        return (messages, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Message> Get(Guid messageId, CancellationToken ct = default)
    {
        var message = await _repository.Get(messageId, _identityProvider.Current.User.UserId, ct);
        if (message == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Message not found");
        }

        return message;
    }
}