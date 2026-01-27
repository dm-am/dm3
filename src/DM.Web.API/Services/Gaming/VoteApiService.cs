using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Gaming.BusinessProcesses.Votes.Creating;
using DM.Services.Gaming.BusinessProcesses.Votes.Deleting;
using DM.Services.Gaming.BusinessProcesses.Votes.Reading;
using DM.Services.Gaming.Dto.Input;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;

namespace DM.Web.API.Services.Gaming;

/// <inheritdoc />
internal class VoteApiService : IVoteApiService
{
    private readonly IVoteReadingService readingService;
    private readonly IVoteCreatingService creatingService;
    private readonly IVoteDeletingService deletingService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public VoteApiService(
        IVoteReadingService readingService,
        IVoteCreatingService creatingService,
        IVoteDeletingService deletingService,
        IMapper mapper)
    {
        this.readingService = readingService;
        this.creatingService = creatingService;
        this.deletingService = deletingService;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Vote>> GetByPost(Guid postId)
    {
        var votes = await readingService.GetByPost(postId);
        return new ListEnvelope<Vote>(votes.Select(mapper.Map<Vote>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Vote>> Get(Guid voteId)
    {
        var vote = await readingService.Get(voteId);
        return new Envelope<Vote>(mapper.Map<Vote>(vote));
    }

    /// <inheritdoc />
    public async Task<Envelope<Vote>> Create(Guid postId, Vote vote)
    {
        var createVote = mapper.Map<CreateVote>(vote);
        createVote.PostId = postId;
        var createdVote = await creatingService.Create(createVote);
        return new Envelope<Vote>(mapper.Map<Vote>(createdVote));
    }

    /// <inheritdoc />
    public Task Delete(Guid voteId) => deletingService.Delete(voteId);
}
