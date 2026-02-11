using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;

namespace DM.Services.Community.BusinessProcesses.Polls.Reading;

/// <inheritdoc />
internal class PollReadingService : IPollReadingService
{
    private readonly IPollReadingRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public PollReadingService(
        IPollReadingRepository repository,
        IDateTimeProvider dateTimeProvider,
        IIdentityProvider identityProvider)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Poll> polls, PagingResult paging)> Get(PagingQuery pagingQuery, bool onlyActive)
    {
        var activeAt = onlyActive ? _dateTimeProvider.Now : (DateTimeOffset?) null;
        var totalCount = await _repository.Count(activeAt);
        var pageSize = _identityProvider.Current.Settings.Paging.EntitiesPerPage;
        var pagingData = new PagingData(pagingQuery, pageSize, (int) totalCount);
        var polls = await _repository.Get(activeAt, pagingData);
        return (polls, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Poll> Get(Guid pollId)
    {
        var poll = await _repository.Get(pollId);
        if (poll == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Poll not found");
        }

        return poll;
    }
}