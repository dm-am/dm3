using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Users;
using DM.Web.API.Shared.Dto;
using DomainPoll = DM.Domain.Community.Features.Polls.Poll;

namespace DM.Web.API.Features.Community.Polls;

public class PollApiService
{
    private readonly IPollService _pollService;
    private readonly IUserLookupService _userLookupService;
    private readonly PollMapper _mapper;

    public PollApiService(
        IPollService pollService,
        IUserLookupService userLookupService,
        PollMapper mapper)
    {
        _pollService = pollService;
        _userLookupService = userLookupService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<Poll>> Get(PollsQuery query)
    {
        var domainQuery = _mapper.ToDomainQuery(query);
        var (polls, paging) = await _pollService.GetListAsync(domainQuery);
        var mappedPolls = await MapPollsAsync(polls);
        return new ListEnvelope<Poll>(mappedPolls, new PagingInfo(paging));
    }

    public async Task<Envelope<Poll>> Get(Guid id)
    {
        var poll = await _pollService.GetAsync(id);
        return new Envelope<Poll>(await MapPollAsync(poll));
    }

    public async Task<Envelope<Poll>> Create(CreatePollRequest request)
    {
        var createPoll = _mapper.ToCreatePoll(request);
        var createdPoll = await _pollService.CreateAsync(createPoll);
        return new Envelope<Poll>(await MapPollAsync(createdPoll));
    }

    public async Task<Envelope<Poll>> Vote(Guid pollId, Guid optionId)
    {
        var poll = await _pollService.VoteAsync(pollId, optionId);
        return new Envelope<Poll>(await MapPollAsync(poll));
    }

    public async Task<Envelope<Poll>> Unvote(Guid pollId)
    {
        var poll = await _pollService.UnvoteAsync(pollId);
        return new Envelope<Poll>(await MapPollAsync(poll));
    }

    public async Task<Envelope<Poll>> Update(Guid id, UpdatePollRequest request)
    {
        var updatePoll = _mapper.ToUpdatePoll(request);
        // From the route, not from the body.
        updatePoll.Id = id;
        var updatedPoll = await _pollService.UpdateAsync(updatePoll);
        return new Envelope<Poll>(await MapPollAsync(updatedPoll));
    }

    public Task Delete(Guid id) => _pollService.DeleteAsync(id);

    /// <summary>
    /// Map multiple domain polls to API polls with batch-loaded voters (avoids DbContext concurrency)
    /// </summary>
    /// <remarks>
    /// Voters are references, so the reference read is enough and no counters are hydrated.
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

        Dictionary<Guid, UserReference> usersDict = new();
        if (allUserIds.Count > 0)
        {
            var users = await _userLookupService.GetReferencesAsync(allUserIds);
            usersDict = users.ToDictionary(u => u.UserId);
        }

        return pollsList
            .Select(poll => _mapper.ToPoll(poll, VotersByOptionId(poll, usersDict)))
            .ToList();
    }

    private async Task<Poll> MapPollAsync(DomainPoll poll) => (await MapPollsAsync([poll])).Single();

    /// <summary>
    /// Pre-mapped voter lists per option for a public poll; null for anonymous
    /// polls and when nothing was loaded, which renders as "no voter lists"
    /// </summary>
    private static Dictionary<Guid, List<UserRef>>? VotersByOptionId(
        DomainPoll poll,
        Dictionary<Guid, UserReference> usersDict)
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
