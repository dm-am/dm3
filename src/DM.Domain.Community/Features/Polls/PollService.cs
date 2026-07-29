using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Community.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Community.Features.Polls;

/// <inheritdoc />
internal class PollService : IPollService
{
    private readonly IValidator<CreatePoll> _createValidator;
    private readonly IValidator<UpdatePoll> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IPollFactory _factory;
    private readonly IPollRepository _repository;
    private readonly IEventProducer _producer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIdentityProvider _identityProvider;

    public PollService(
        IValidator<CreatePoll> createValidator,
        IValidator<UpdatePoll> updateValidator,
        IIntentionManager intentionManager,
        IPollFactory factory,
        IPollRepository repository,
        IEventProducer producer,
        IDateTimeProvider dateTimeProvider,
        IIdentityProvider identityProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _factory = factory;
        _repository = repository;
        _producer = producer;
        _dateTimeProvider = dateTimeProvider;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Poll> CreateAsync(CreatePoll createPoll)
    {
        await _createValidator.ValidateAndThrowAsync(createPoll);
        _intentionManager.ThrowIfForbidden(PollIntention.Create);

        var poll = _factory.Create(createPoll);
        var result = await _repository.Create(poll);
        await _producer.SendAsync(EventType.NewPoll, result.Id);

        return result;
    }

    /// <inheritdoc />
    public async Task<Poll> GetAsync(Guid pollId)
    {
        var poll = await _repository.Get(pollId);
        if (poll == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Poll not found");
        }

        return poll;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Poll> Polls, PagingResult Paging)> GetListAsync(PollsQuery query)
    {
        var totalCount = await _repository.Count(query);
        var pageSize = _identityProvider.Current.Settings.Paging.EntitiesPerPage;
        var pagingData = new PagingData(query, pageSize, (int)totalCount);
        var polls = await _repository.Get(query, pagingData);
        return (polls, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Poll> UpdateAsync(UpdatePoll updatePoll)
    {
        await _updateValidator.ValidateAndThrowAsync(updatePoll);
        var existingPoll = await GetAsync(updatePoll.Id);
        _intentionManager.ThrowIfForbidden(PollIntention.Edit, existingPoll);

        return await _repository.Update(
            updatePoll.Id,
            updatePoll.Title,
            updatePoll.Details,
            updatePoll.StartsUtc,
            updatePoll.EndsUtc,
            updatePoll.IsAnonymous);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid pollId)
    {
        var poll = await GetAsync(pollId);
        _intentionManager.ThrowIfForbidden(PollIntention.Delete, poll);
        await _repository.Delete(pollId);
    }

    /// <inheritdoc />
    public async Task<Poll> VoteAsync(Guid pollId, Guid optionId)
    {
        var poll = await GetAsync(pollId);
        _intentionManager.ThrowIfForbidden(PollIntention.Vote, (poll, optionId));

        return await _repository.Vote(pollId, optionId, _identityProvider.Current.User.UserId);
    }

    /// <inheritdoc />
    public async Task<Poll> UnvoteAsync(Guid pollId)
    {
        var poll = await GetAsync(pollId);
        _intentionManager.ThrowIfForbidden(PollIntention.Unvote, poll);

        return await _repository.Unvote(pollId, _identityProvider.Current.User.UserId);
    }
}
