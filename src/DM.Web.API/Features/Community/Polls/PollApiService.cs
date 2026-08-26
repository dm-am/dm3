using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Users;
using DM.Web.API.Shared.Dto;
using DomainPoll = DM.Domain.Community.Features.Polls.Poll;

namespace DM.Web.API.Features.Community.Polls;

/// <inheritdoc />
internal class PollApiService : IPollApiService
{
    private readonly IPollService _pollService;
    private readonly IUserLookupService _userLookupService;
    private readonly PollMapper _mapper;

    /// <inheritdoc />
    public PollApiService(
        IPollService pollService,
        IUserLookupService userLookupService,
        PollMapper mapper)
    {
        _pollService = pollService;
        _userLookupService = userLookupService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Poll>> Get(PollsQuery query)
    {
        var domainQuery = _mapper.ToDomainQuery(query);
        var (polls, paging) = await _pollService.GetListAsync(domainQuery);
        var mappedPolls = await MapPollsAsync(polls);
        return new ListEnvelope<Poll>(mappedPolls, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Get(Guid id)
    {
        var poll = await _pollService.GetAsync(id);
        return new Envelope<Poll>(await MapPollAsync(poll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Create(CreatePollRequest request)
    {
        var createPoll = _mapper.ToCreatePoll(request);
        // The very list the request arrived with, not a copy of it.
        createPoll.Options = request.Options;
        var createdPoll = await _pollService.CreateAsync(createPoll);
        return new Envelope<Poll>(await MapPollAsync(createdPoll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Vote(Guid pollId, Guid optionId)
    {
        var poll = await _pollService.VoteAsync(pollId, optionId);
        return new Envelope<Poll>(await MapPollAsync(poll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Unvote(Guid pollId)
    {
        var poll = await _pollService.UnvoteAsync(pollId);
        return new Envelope<Poll>(await MapPollAsync(poll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Update(Guid id, UpdatePollRequest request)
    {
        var updatePoll = _mapper.ToUpdatePoll(request);
        // From the route, not from the body.
        updatePoll.Id = id;
        var updatedPoll = await _pollService.UpdateAsync(updatePoll);
        return new Envelope<Poll>(await MapPollAsync(updatedPoll));
    }

    /// <inheritdoc />
    public Task Delete(Guid id) => _pollService.DeleteAsync(id);

    /// <summary>
    /// Map multiple domain polls to API polls with batch-loaded voters (avoids DbContext concurrency)
    /// </summary>
    /// <remarks>
    /// Voters render as UserRef: id, name, last activity, role, newbie flag. That
    /// is what the reference read returns, and none of it comes from the counter
    /// hydration a full user read performs — so every public poll render used to
    /// pay twenty-one aggregate queries to fill fields the response never carried.
    /// </remarks>
    private async Task<IEnumerable<Poll>> MapPollsAsync(IEnumerable<DomainPoll> polls)
    {
        var pollsList = polls.ToList();

        // Batch-load all voters for all public polls in one query
        var publicPolls = pollsList.Where(p => !p.IsAnonymous).ToList();
        var allUserIds = publicPolls
            .SelectMany(p => p.Options.SelectMany(o => o.UserIds))
            .Distinct()
            .ToList();

        Dictionary<Guid, Domain.Core.Dto.UserReference> usersDict = new();
        if (allUserIds.Count > 0)
        {
            var users = await _userLookupService.GetReferencesAsync(allUserIds);
            usersDict = users.ToDictionary(u => u.UserId);
        }

        return pollsList
            .Select(poll => _mapper.ToPoll(poll, VotersByOptionId(poll, usersDict)))
            .ToList();
    }

    /// <summary>
    /// Map single domain poll to API poll with voters batch-loading
    /// </summary>
    private async Task<Poll> MapPollAsync(DomainPoll poll)
    {
        Dictionary<Guid, Domain.Core.Dto.UserReference> usersDict = new();

        // Batch-load voters for public polls
        if (!poll.IsAnonymous)
        {
            var allUserIds = poll.Options
                .SelectMany(o => o.UserIds)
                .Distinct()
                .ToList();

            if (allUserIds.Count > 0)
            {
                var users = await _userLookupService.GetReferencesAsync(allUserIds);
                usersDict = users.ToDictionary(u => u.UserId);
            }
        }

        return _mapper.ToPoll(poll, VotersByOptionId(poll, usersDict));
    }

    /// <summary>
    /// Pre-mapped voter lists per option for a public poll; null for anonymous
    /// polls and when nothing was loaded, which renders as "no voter lists"
    /// </summary>
    private static Dictionary<Guid, List<UserRef>>? VotersByOptionId(
        DomainPoll poll,
        Dictionary<Guid, Domain.Core.Dto.UserReference> usersDict)
    {
        if (poll.IsAnonymous || usersDict.Count == 0)
        {
            return null;
        }

        return poll.Options.ToDictionary(
            o => o.Id,
            o => o.UserIds
                .Select(id => usersDict.GetValueOrDefault(id))
                .Where(u => u != null)
                .Select(u => UserRefMappers.ToUserRef(u!))
                .ToList());
    }
}
