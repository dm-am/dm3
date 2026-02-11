using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Lifecycle;

/// <inheritdoc />
internal class GlobalChatEventLifecycleService : IGlobalChatEventLifecycleService
{
    private readonly IGlobalChatEventReadingService _readingService;
    private readonly IGlobalChatEventReadingRepository _readingRepository;
    private readonly IGlobalChatEventLifecycleRepository _lifecycleRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IInvokedEventProducer _eventProducer;

    /// <inheritdoc />
    public GlobalChatEventLifecycleService(
        IGlobalChatEventReadingService readingService,
        IGlobalChatEventReadingRepository readingRepository,
        IGlobalChatEventLifecycleRepository lifecycleRepository,
        IIntentionManager intentionManager,
        IDateTimeProvider dateTimeProvider,
        IInvokedEventProducer eventProducer)
    {
        _readingService = readingService;
        _readingRepository = readingRepository;
        _lifecycleRepository = lifecycleRepository;
        _intentionManager = intentionManager;
        _dateTimeProvider = dateTimeProvider;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task<GlobalChatEvent> Start(Guid eventId, CancellationToken ct = default)
    {
        var GlobalChatEvent = await _readingService.Get(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Start, GlobalChatEvent);

        // Check if there's already an active event
        if (await _readingRepository.HasActiveEvent())
        {
            throw new HttpException(HttpStatusCode.Conflict, "There is already an active chat event");
        }

        var result = await _lifecycleRepository.UpdateStatus(
            eventId,
            GlobalChatEventStatus.Live,
            startedAt: _dateTimeProvider.Now,
            endedAt: null,
            ct).ConfigureAwait(false);

        await _eventProducer.Send(EventType.GlobalChatEventStarted, eventId).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public async Task<GlobalChatEvent> End(Guid eventId, CancellationToken ct = default)
    {
        var GlobalChatEvent = await _readingService.Get(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.End, GlobalChatEvent);

        var result = await _lifecycleRepository.UpdateStatus(
            eventId,
            GlobalChatEventStatus.Ended,
            startedAt: null,
            endedAt: _dateTimeProvider.Now,
            ct).ConfigureAwait(false);

        await _eventProducer.Send(EventType.GlobalChatEventEnded, eventId).ConfigureAwait(false);

        return result;
    }
}
