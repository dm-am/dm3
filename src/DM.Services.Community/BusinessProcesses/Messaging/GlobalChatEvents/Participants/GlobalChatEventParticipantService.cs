using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Creating;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Participants;

/// <inheritdoc />
internal class GlobalChatEventParticipantService : IGlobalChatEventParticipantService
{
    private readonly IGlobalChatEventReadingService _readingService;
    private readonly IGlobalChatEventParticipantRepository _repository;
    private readonly IGlobalChatEventFactory _factory;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IInvokedEventProducer _eventProducer;

    /// <inheritdoc />
    public GlobalChatEventParticipantService(
        IGlobalChatEventReadingService readingService,
        IGlobalChatEventParticipantRepository repository,
        IGlobalChatEventFactory factory,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IInvokedEventProducer eventProducer)
    {
        _readingService = readingService;
        _repository = repository;
        _factory = factory;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public Task<IEnumerable<GlobalChatEventParticipant>> GetParticipants(Guid eventId) =>
        _repository.GetParticipants(eventId);

    /// <inheritdoc />
    public async Task<GlobalChatEventParticipant> Join(Guid eventId, CancellationToken ct = default)
    {
        var GlobalChatEvent = await _readingService.Get(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Join, GlobalChatEvent);

        var userId = _identityProvider.Current.User.UserId;

        if (await _repository.IsParticipant(eventId, userId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "User is already a participant");
        }

        var participant = _factory.CreateParticipant(eventId, userId, isOrganizer: false);
        var result = await _repository.Add(participant, ct).ConfigureAwait(false);

        await _eventProducer.Send(EventType.GlobalChatEventParticipantJoined, eventId).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public async Task Leave(Guid eventId, CancellationToken ct = default)
    {
        var GlobalChatEvent = await _readingService.Get(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Leave, GlobalChatEvent);

        var userId = _identityProvider.Current.User.UserId;

        await _repository.Remove(eventId, userId, ct).ConfigureAwait(false);

        await _eventProducer.Send(EventType.GlobalChatEventParticipantLeft, eventId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GlobalChatEventParticipant> AddParticipant(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var GlobalChatEvent = await _readingService.Get(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.AddParticipant, GlobalChatEvent);

        if (await _repository.IsParticipant(eventId, userId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "User is already a participant");
        }

        var participant = _factory.CreateParticipant(eventId, userId, isOrganizer: false);
        var result = await _repository.Add(participant, ct).ConfigureAwait(false);

        await _eventProducer.Send(EventType.GlobalChatEventParticipantJoined, eventId).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public async Task RemoveParticipant(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var GlobalChatEvent = await _readingService.Get(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.RemoveParticipant, GlobalChatEvent);

        await _repository.Remove(eventId, userId, ct).ConfigureAwait(false);

        await _eventProducer.Send(EventType.GlobalChatEventParticipantLeft, eventId).ConfigureAwait(false);
    }
}
