using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Deleting;

/// <inheritdoc />
internal class GlobalChatEventDeletingService : IGlobalChatEventDeletingService
{
    private readonly IGlobalChatEventReadingService _readingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IGlobalChatEventDeletingRepository _repository;

    /// <inheritdoc />
    public GlobalChatEventDeletingService(
        IGlobalChatEventReadingService readingService,
        IIntentionManager intentionManager,
        IGlobalChatEventDeletingRepository repository)
    {
        _readingService = readingService;
        _intentionManager = intentionManager;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task Delete(Guid eventId, CancellationToken ct = default)
    {
        var GlobalChatEvent = await _readingService.Get(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Delete, GlobalChatEvent);

        await _repository.Delete(eventId, ct).ConfigureAwait(false);
    }
}
