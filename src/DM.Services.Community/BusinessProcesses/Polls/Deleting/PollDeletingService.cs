using System;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Polls.Reading;

namespace DM.Services.Community.BusinessProcesses.Polls.Deleting;

/// <inheritdoc />
internal class PollDeletingService : IPollDeletingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IPollReadingService _readingService;
    private readonly IPollDeletingRepository _repository;

    /// <inheritdoc />
    public PollDeletingService(
        IIntentionManager intentionManager,
        IPollReadingService readingService,
        IPollDeletingRepository repository)
    {
        _intentionManager = intentionManager;
        _readingService = readingService;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task Delete(Guid pollId)
    {
        var poll = await _readingService.Get(pollId);
        _intentionManager.ThrowIfForbidden(PollIntention.Delete, poll);
        await _repository.Delete(pollId);
    }
}
