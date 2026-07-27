using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Users;
using DM.Web.API.Shared.Dto;
using DomainPoll = DM.Domain.Community.Features.Polls.Poll;
using DomainPollsQuery = DM.Domain.Community.Features.Polls.PollsQuery;

namespace DM.Web.API.Features.Community.Polls;

/// <inheritdoc />
internal class PollApiService : IPollApiService
{
    private readonly IPollService pollService;
    private readonly IUserReadRepository userRepository;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public PollApiService(
        IPollService pollService,
        IUserReadRepository userRepository,
        IMapper mapper)
    {
        this.pollService = pollService;
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Poll>> Get(PollsQuery query)
    {
        var domainQuery = new DomainPollsQuery
        {
            Skip = query.Skip,
            Take = query.Take,
            Status = query.Status,
            Search = query.Search,
            StartsFromUtc = query.StartsFromUtc,
            StartsToUtc = query.StartsToUtc,
            EndsFromUtc = query.EndsFromUtc,
            EndsToUtc = query.EndsToUtc,
            SortBy = query.SortBy,
            SortOrder = query.SortOrder,
            IsAnonymous = query.IsAnonymous
        };
        var (polls, paging) = await pollService.GetListAsync(domainQuery);
        var mappedPolls = await MapPollsAsync(polls);
        return new ListEnvelope<Poll>(mappedPolls, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Get(Guid id)
    {
        var poll = await pollService.GetAsync(id);
        return new Envelope<Poll>(await MapPollAsync(poll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Create(CreatePollRequest request)
    {
        var createPoll = new CreatePoll
        {
            Title = request.Title,
            Details = request.Details,
            StartsUtc = request.StartsUtc,
            EndsUtc = request.EndsUtc,
            IsAnonymous = request.IsAnonymous,
            Options = request.Options
        };
        var createdPoll = await pollService.CreateAsync(createPoll);
        return new Envelope<Poll>(await MapPollAsync(createdPoll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Vote(Guid pollId, Guid optionId)
    {
        var poll = await pollService.VoteAsync(pollId, optionId);
        return new Envelope<Poll>(await MapPollAsync(poll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Unvote(Guid pollId)
    {
        var poll = await pollService.UnvoteAsync(pollId);
        return new Envelope<Poll>(await MapPollAsync(poll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Update(Guid id, UpdatePollRequest request)
    {
        var updatePoll = new UpdatePoll
        {
            Id = id,
            Title = request.Title,
            Details = request.Details,
            StartsUtc = request.StartsUtc,
            EndsUtc = request.EndsUtc,
            IsAnonymous = request.IsAnonymous
        };
        var updatedPoll = await pollService.UpdateAsync(updatePoll);
        return new Envelope<Poll>(await MapPollAsync(updatedPoll));
    }

    /// <inheritdoc />
    public Task Delete(Guid id) => pollService.DeleteAsync(id);

    /// <summary>
    /// Map multiple domain polls to API polls with batch-loaded voters (avoids DbContext concurrency)
    /// </summary>
    private async Task<IEnumerable<Poll>> MapPollsAsync(IEnumerable<DomainPoll> polls)
    {
        var pollsList = polls.ToList();

        // Batch-load all voters for all public polls in one query
        var publicPolls = pollsList.Where(p => !p.IsAnonymous).ToList();
        var allUserIds = publicPolls
            .SelectMany(p => p.Options.SelectMany(o => o.UserIds))
            .Distinct()
            .ToList();

        Dictionary<Guid, Domain.Core.Dto.GeneralUser> usersDict = new();
        if (allUserIds.Count > 0)
        {
            var users = await userRepository.GetUsersAsync(allUserIds);
            usersDict = users.ToDictionary(u => u.UserId);
        }

        // Map all polls sequentially (no parallel DbContext access)
        return pollsList.Select(poll => MapPollWithUsers(poll, usersDict)).ToList();
    }

    /// <summary>
    /// Map domain poll to API poll with pre-loaded users dictionary
    /// </summary>
    private Poll MapPollWithUsers(DomainPoll poll, Dictionary<Guid, Domain.Core.Dto.GeneralUser> usersDict)
    {
        Dictionary<Guid, List<UserRef>>? votersByOptionId = null;

        if (!poll.IsAnonymous)
        {
            votersByOptionId = poll.Options.ToDictionary(
                o => o.Id,
                o => o.UserIds
                    .Select(id => usersDict.GetValueOrDefault(id))
                    .Where(u => u != null)
                    .Select(u => mapper.Map<UserRef>(u!))
                    .ToList());
        }

        return mapper.Map<Poll>(poll, opts =>
        {
            opts.Items["IsAnonymous"] = poll.IsAnonymous;
            if (votersByOptionId != null)
                opts.Items["VotersByOptionId"] = votersByOptionId;
        });
    }

    /// <summary>
    /// Map single domain poll to API poll with voters batch-loading
    /// </summary>
    private async Task<Poll> MapPollAsync(DomainPoll poll)
    {
        Dictionary<Guid, List<UserRef>>? votersByOptionId = null;

        // Batch-load voters for public polls
        if (!poll.IsAnonymous)
        {
            var allUserIds = poll.Options
                .SelectMany(o => o.UserIds)
                .Distinct()
                .ToList();

            if (allUserIds.Count > 0)
            {
                var users = await userRepository.GetUsersAsync(allUserIds);
                var usersDict = users.ToDictionary(u => u.UserId);

                votersByOptionId = poll.Options.ToDictionary(
                    o => o.Id,
                    o => o.UserIds
                        .Select(id => usersDict.GetValueOrDefault(id))
                        .Where(u => u != null)
                        .Select(u => mapper.Map<UserRef>(u!))
                        .ToList());
            }
        }

        return mapper.Map<Poll>(poll, opts =>
        {
            opts.Items["IsAnonymous"] = poll.IsAnonymous;
            if (votersByOptionId != null)
                opts.Items["VotersByOptionId"] = votersByOptionId;
        });
    }
}
