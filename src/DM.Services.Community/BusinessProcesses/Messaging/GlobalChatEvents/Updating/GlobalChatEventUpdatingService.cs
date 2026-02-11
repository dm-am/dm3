using System.Threading;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using DM.Services.DataAccess.RelationalStorage;
using DbGlobalChatEvent = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEvent;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Updating;

/// <inheritdoc />
internal class GlobalChatEventUpdatingService : IGlobalChatEventUpdatingService
{
    private readonly IGlobalChatEventReadingService _readingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IGlobalChatEventUpdatingRepository _repository;

    /// <inheritdoc />
    public GlobalChatEventUpdatingService(
        IGlobalChatEventReadingService readingService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IGlobalChatEventUpdatingRepository repository)
    {
        _readingService = readingService;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<GlobalChatEvent> Update(UpdateGlobalChatEvent updateGlobalChatEvent, CancellationToken ct = default)
    {
        var GlobalChatEvent = await _readingService.Get(updateGlobalChatEvent.Id).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Update, GlobalChatEvent);

        var updateBuilder = _updateBuilderFactory.Create<DbGlobalChatEvent>(updateGlobalChatEvent.Id)
            .MaybeField(e => e.Title, updateGlobalChatEvent.Title)
            .MaybeField(e => e.Description, updateGlobalChatEvent.Description);

        if (updateGlobalChatEvent.StartsAt.HasValue)
        {
            updateBuilder.Field(e => e.StartsAtUtc, updateGlobalChatEvent.StartsAt.Value);
        }

        if (updateGlobalChatEvent.Duration.HasValue)
        {
            updateBuilder.Field(e => e.Duration, updateGlobalChatEvent.Duration);
        }

        if (updateGlobalChatEvent.IsOpen.HasValue)
        {
            updateBuilder.Field(e => e.IsOpen, updateGlobalChatEvent.IsOpen.Value);
        }

        await _repository.Update(updateBuilder, ct).ConfigureAwait(false);

        return await _readingService.Get(updateGlobalChatEvent.Id).ConfigureAwait(false);
    }
}
