using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <inheritdoc />
internal class GlobalChatEventService : IGlobalChatEventService
{
    private readonly IValidator<CreateGlobalChatEvent> _createValidator;
    private readonly IValidator<UpdateGlobalChatEvent> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IGlobalChatEventFactory _factory;
    private readonly IGlobalChatEventRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;

    public GlobalChatEventService(
        IValidator<CreateGlobalChatEvent> createValidator,
        IValidator<UpdateGlobalChatEvent> updateValidator,
        IIntentionManager intentionManager,
        IGlobalChatEventFactory factory,
        IGlobalChatEventRepository repository,
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        IEventProducer eventProducer)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _factory = factory;
        _repository = repository;
        _identityProvider = identityProvider;
        _dateTimeProvider = dateTimeProvider;
        _eventProducer = eventProducer;
    }

    // ═══ CREATE ═══

    /// <inheritdoc />
    public async Task<GlobalChatEvent> CreateAsync(CreateGlobalChatEvent createGlobalChatEvent, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createGlobalChatEvent, ct).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Create);

        var userId = _identityProvider.Current.User.UserId;
        // Event descriptions render on the Comment surface where [mod] is a
        // green mod block. Event authoring is moderator-gated already, so this
        // is defensive: strip [mod] if the author is somehow below Moderator.
        if (!string.IsNullOrEmpty(createGlobalChatEvent.Description))
            createGlobalChatEvent.Description = ModBlockSanitizer.SanitizeForAuthor(
                createGlobalChatEvent.Description, _identityProvider.Current.User.Role);
        var chatEvent = _factory.Create(createGlobalChatEvent, userId);
        var creatorParticipant = _factory.CreateParticipant(chatEvent.GlobalChatEventId, userId, isOrganizer: true);

        return await _repository.Create(chatEvent, creatorParticipant, ct).ConfigureAwait(false);
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public async Task<GlobalChatEvent> GetAsync(Guid eventId)
    {
        var chatEvent = await _repository.Get(eventId).ConfigureAwait(false);
        if (chatEvent == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Эвент не найден");
        }

        return chatEvent;
    }

    /// <inheritdoc />
    public Task<GlobalChatEvent?> GetActiveEventAsync() => _repository.GetActiveEvent();

    /// <inheritdoc />
    public Task<IEnumerable<GlobalChatEvent>> GetAllAsync() =>
        _repository.GetByStatus(GlobalChatEventStatus.Live, GlobalChatEventStatus.Scheduled);

    // ═══ UPDATE ═══

    /// <inheritdoc />
    public async Task<GlobalChatEvent> UpdateAsync(UpdateGlobalChatEvent updateGlobalChatEvent, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(updateGlobalChatEvent, ct).ConfigureAwait(false);
        var chatEvent = await GetAsync(updateGlobalChatEvent.Id).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Update, chatEvent);

        var description = updateGlobalChatEvent.Description;
        // Event descriptions render on the Comment surface where [mod] is a
        // green mod block; defensive strip for a below-Moderator editor.
        if (!string.IsNullOrEmpty(description))
            description = ModBlockSanitizer.SanitizeForAuthor(
                description, _identityProvider.Current.User.Role);

        var updateEntity = new UpdateGlobalChatEventEntity
        {
            GlobalChatEventId = updateGlobalChatEvent.Id,
            Title = updateGlobalChatEvent.Title,
            Description = description,
            StartsUtc = updateGlobalChatEvent.StartsUtc,
            Duration = updateGlobalChatEvent.Duration,
            IsOpen = updateGlobalChatEvent.IsOpen
        };

        await _repository.Update(updateEntity, ct).ConfigureAwait(false);

        return await GetAsync(updateGlobalChatEvent.Id).ConfigureAwait(false);
    }

    // ═══ DELETE ═══

    /// <inheritdoc />
    public async Task DeleteAsync(Guid eventId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Delete, chatEvent);

        await _repository.Delete(eventId, ct).ConfigureAwait(false);
    }

    // ═══ LIFECYCLE ═══

    /// <inheritdoc />
    public async Task<GlobalChatEvent> StartAsync(Guid eventId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Start, chatEvent);

        // Check if there's already an active event
        if (await _repository.HasActiveEvent())
        {
            throw new HttpException(HttpStatusCode.Conflict, "Сейчас уже идет другой эвент");
        }

        var result = await _repository.UpdateStatus(
            eventId,
            GlobalChatEventStatus.Live,
            startedAt: _dateTimeProvider.Now,
            endedAt: null,
            ct).ConfigureAwait(false);

        await _eventProducer.SendAsync(EventType.GlobalChatEventStarted, eventId).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public async Task<GlobalChatEvent> EndAsync(Guid eventId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.End, chatEvent);

        var result = await _repository.UpdateStatus(
            eventId,
            GlobalChatEventStatus.Ended,
            startedAt: null,
            endedAt: _dateTimeProvider.Now,
            ct).ConfigureAwait(false);

        await _eventProducer.SendAsync(EventType.GlobalChatEventEnded, eventId).ConfigureAwait(false);

        return result;
    }

    // ═══ PARTICIPANTS ═══

    /// <summary>
    /// One person joins an event, whether they asked or an organiser added them.
    /// </summary>
    /// <remarks>
    /// A second row for somebody already on the list is a 409 and not a silent
    /// success: the caller is told the state it wanted is the state that already
    /// holds, rather than being handed a duplicate participant.
    /// </remarks>
    private async Task<GlobalChatEventParticipant> AddParticipant(
        Guid eventId, Guid userId, CancellationToken ct)
    {
        if (await _repository.IsParticipant(eventId, userId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.UserAlreadyParticipant);
        }

        var participant = _factory.CreateParticipant(eventId, userId, isOrganizer: false);
        var result = await _repository.AddParticipant(participant, ct).ConfigureAwait(false);

        await _eventProducer.SendAsync(EventType.GlobalChatEventParticipantJoined, eventId).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public async Task<GlobalChatEventParticipant> JoinAsync(Guid eventId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Join, chatEvent);

        var userId = _identityProvider.Current.User.UserId;

        return await AddParticipant(eventId, userId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LeaveAsync(Guid eventId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Leave, chatEvent);

        var userId = _identityProvider.Current.User.UserId;

        await _repository.RemoveParticipant(eventId, userId, ct).ConfigureAwait(false);

        await _eventProducer.SendAsync(EventType.GlobalChatEventParticipantLeft, eventId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GlobalChatEventParticipant> AddParticipantAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.AddParticipant, chatEvent);

        return await AddParticipant(eventId, userId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveParticipantAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.RemoveParticipant, chatEvent);

        await _repository.RemoveParticipant(eventId, userId, ct).ConfigureAwait(false);

        await _eventProducer.SendAsync(EventType.GlobalChatEventParticipantLeft, eventId).ConfigureAwait(false);
    }
}
