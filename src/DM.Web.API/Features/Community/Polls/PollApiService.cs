using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.Polls;
using DM.Web.API.Shared.Dto;
using DomainPoll = DM.Domain.Community.Features.Polls.Poll;

namespace DM.Web.API.Features.Community.Polls;

/// <inheritdoc />
internal class PollApiService : IPollApiService
{
    private readonly IPollService pollService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public PollApiService(
        IPollService pollService,
        IMapper mapper)
    {
        this.pollService = pollService;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Poll>> Get(PollsQuery query)
    {
        var (polls, paging) = await pollService.GetListAsync(query, query.OnlyActive);
        return new ListEnvelope<Poll>(polls.Select(mapper.Map<Poll>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Get(Guid id)
    {
        var poll = await pollService.GetAsync(id);
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
        var createdPoll = await pollService.CreateAsync(createPoll);
        return new Envelope<Poll>(mapper.Map<Poll>(createdPoll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Vote(Guid pollId, Guid optionId)
    {
        var poll = await pollService.VoteAsync(pollId, optionId);
        return new Envelope<Poll>(mapper.Map<Poll>(poll));
    }

    /// <inheritdoc />
    public async Task<Envelope<Poll>> Unvote(Guid pollId)
    {
        var poll = await pollService.UnvoteAsync(pollId);
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
        var updatedPoll = await pollService.UpdateAsync(updatePoll);
        return new Envelope<Poll>(mapper.Map<Poll>(updatedPoll));
    }

    /// <inheritdoc />
    public Task Delete(Guid id) => pollService.DeleteAsync(id);
}
