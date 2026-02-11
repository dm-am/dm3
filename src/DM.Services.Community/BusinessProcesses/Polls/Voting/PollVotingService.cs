using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Polls.Reading;

namespace DM.Services.Community.BusinessProcesses.Polls.Voting;

/// <inheritdoc />
internal class PollVotingService : IPollVotingService
{
    private readonly IPollReadingService _readingService;
    private readonly IPollVotingRepository _repository;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public PollVotingService(
        IPollReadingService readingService,
        IPollVotingRepository repository,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider)
    {
        _readingService = readingService;
        _repository = repository;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
    }
        
    /// <inheritdoc />
    public async Task<Poll> Vote(Guid pollId, Guid optionId)
    {
        var poll = await _readingService.Get(pollId);
        _intentionManager.ThrowIfForbidden(PollIntention.Vote, (poll, optionId));

        return await _repository.Vote(pollId, optionId, _identityProvider.Current.User.UserId);
    }

    /// <inheritdoc />
    public async Task<Poll> Unvote(Guid pollId)
    {
        var poll = await _readingService.Get(pollId);
        _intentionManager.ThrowIfForbidden(PollIntention.Unvote, poll);

        return await _repository.Unvote(pollId, _identityProvider.Current.User.UserId);
    }
}