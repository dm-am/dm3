using System;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Votes.Reading;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Deleting;

/// <inheritdoc />
internal class VoteDeletingService : IVoteDeletingService
{
    private readonly IVoteReadingService readingService;
    private readonly IIntentionManager intentionManager;
    private readonly IVoteDeletingRepository repository;

    /// <inheritdoc />
    public VoteDeletingService(
        IVoteReadingService readingService,
        IIntentionManager intentionManager,
        IVoteDeletingRepository repository)
    {
        this.readingService = readingService;
        this.intentionManager = intentionManager;
        this.repository = repository;
    }

    /// <inheritdoc />
    public async Task Delete(Guid voteId)
    {
        var vote = await readingService.Get(voteId);
        intentionManager.ThrowIfForbidden(VoteIntention.Delete, vote);
        await repository.Delete(voteId);
    }
}
