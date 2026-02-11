using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Polls.Creating;
using DM.Services.Community.BusinessProcesses.Polls.Deleting;
using DM.Services.Community.BusinessProcesses.Polls.Reading;
using DM.Services.Community.BusinessProcesses.Polls.Updating;
using DM.Services.Community.BusinessProcesses.Polls.Voting;
using DM.Web.API.Dto.Community;
using DM.Web.API.Dto.Contracts;
using Poll = DM.Web.API.Dto.Community.Poll;

namespace DM.Web.API.Services.Community;

/// <inheritdoc />
internal class PollApiService : IPollApiService
{
    private readonly IPollReadingService readingService;
    private readonly IPollCreatingService creatingService;
    private readonly IPollUpdatingService updatingService;
    private readonly IPollDeletingService deletingService;
    private readonly IPollVotingService votingService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public PollApiService(
        IPollReadingService readingService,
        IPollCreatingService creatingService,
        IPollUpdatingService updatingService,
        IPollDeletingService deletingService,
        IPollVotingService votingService,
        IMapper mapper)
    {
        this.readingService = readingService;
        this.creatingService = creatingService;
        this.updatingService = updatingService;
        this.deletingService = deletingService;
        this.votingService = votingService;
        this.mapper = mapper;
    }
        
    /// <inheritdoc />
    public async Task<ListEnvelope<Poll>> Get(PollsQuery query)
    {
        var (polls, paging) = await readingService.Get(query, query.OnlyActive);
        return new ListEnvelope<Poll>(polls.Select(mapper.Map<Poll>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Get(Guid id)
    {
        var poll = await readingService.Get(id);
        return new Envelope<Poll>(mapper.Map<Poll>(poll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Create(CreatePollRequest request)
    {
        var createPoll = new CreatePoll
        {
            Title = request.Title,
            EndDate = DateTimeOffset.UtcNow.AddDays(request.DurationDays),
            Options = request.Options
        };
        var createdPoll = await creatingService.Create(createPoll);
        return new Envelope<Poll>(mapper.Map<Poll>(createdPoll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Vote(Guid pollId, Guid optionId)
    {
        var poll = await votingService.Vote(pollId, optionId);
        return new Envelope<Poll>(mapper.Map<Poll>(poll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Unvote(Guid pollId)
    {
        var poll = await votingService.Unvote(pollId);
        return new Envelope<Poll>(mapper.Map<Poll>(poll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Update(Guid id, UpdatePollRequest request)
    {
        var updatePoll = new UpdatePoll
        {
            Id = id,
            Title = request.Title,
            EndDate = request.EndsUtc
        };
        var updatedPoll = await updatingService.Update(updatePoll);
        return new Envelope<Poll>(mapper.Map<Poll>(updatedPoll));
    }

    /// <inheritdoc />
    public Task Delete(Guid id) => deletingService.Delete(id);
}