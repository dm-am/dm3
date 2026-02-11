using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

/// <inheritdoc />
internal class GlobalChatEventReadingService : IGlobalChatEventReadingService
{
    private readonly IGlobalChatEventReadingRepository _repository;

    /// <inheritdoc />
    public GlobalChatEventReadingService(
        IGlobalChatEventReadingRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<GlobalChatEvent> Get(Guid eventId)
    {
        var GlobalChatEvent = await _repository.Get(eventId).ConfigureAwait(false);
        if (GlobalChatEvent == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Chat event not found");
        }

        return GlobalChatEvent;
    }

    /// <inheritdoc />
    public Task<GlobalChatEvent?> GetActiveEvent() => _repository.GetActiveEvent();

    /// <inheritdoc />
    public Task<IEnumerable<GlobalChatEvent>> GetUpcomingEvents() => _repository.GetUpcomingEvents();

    /// <inheritdoc />
    public Task<IEnumerable<GlobalChatEvent>> GetAll() =>
        _repository.GetByStatus(GlobalChatEventStatus.Live, GlobalChatEventStatus.Scheduled);
}
