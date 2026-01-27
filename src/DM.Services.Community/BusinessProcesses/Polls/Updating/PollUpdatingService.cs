using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Polls.Reading;

namespace DM.Services.Community.BusinessProcesses.Polls.Updating;

/// <inheritdoc />
internal class PollUpdatingService : IPollUpdatingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IPollReadingService _readingService;
    private readonly IPollUpdatingRepository _repository;

    /// <inheritdoc />
    public PollUpdatingService(
        IIntentionManager intentionManager,
        IPollReadingService readingService,
        IPollUpdatingRepository repository)
    {
        _intentionManager = intentionManager;
        _readingService = readingService;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<Poll> Update(UpdatePoll updatePoll)
    {
        var existingPoll = await _readingService.Get(updatePoll.Id);
        _intentionManager.ThrowIfForbidden(PollIntention.Edit, existingPoll);

        return await _repository.UpdatePoll(
            updatePoll.Id,
            updatePoll.Title,
            updatePoll.EndDate);
    }
}
