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

    /// <inheritdoc />
    public async Task<GlobalChatEvent> CreateAsync(CreateGlobalChatEvent createGlobalChatEvent, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createGlobalChatEvent, ct);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Create);

        var userId = _identityProvider.Current.User.UserId;
        var chatEvent = _factory.Create(createGlobalChatEvent, userId);
        var creatorParticipant = _factory.CreateParticipant(chatEvent.GlobalChatEventId, userId, isOrganizer: true);

        return await _repository.Create(chatEvent, creatorParticipant, ct);
    }

    /// <inheritdoc />
    public async Task<GlobalChatEvent> GetAsync(Guid eventId)
    {
        var chatEvent = await _repository.Get(eventId);
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

    /// <inheritdoc />
    public async Task<GlobalChatEvent> UpdateAsync(UpdateGlobalChatEvent updateGlobalChatEvent, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(updateGlobalChatEvent, ct);
        var chatEvent = await GetAsync(updateGlobalChatEvent.Id);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Update, chatEvent);

        var description = updateGlobalChatEvent.Description;
        // The description renders [mod], and an organizer demoted since creation loses it.
        if (!string.IsNullOrEmpty(description))
            description = ModBlockSanitizer.SanitizeForAuthor(description, _identityProvider.Current.User.Role);

        var updateEntity = new UpdateGlobalChatEventEntity
        {
            GlobalChatEventId = updateGlobalChatEvent.Id,
            Title = updateGlobalChatEvent.Title,
            Description = description,
            StartsUtc = updateGlobalChatEvent.StartsUtc,
            Duration = updateGlobalChatEvent.Duration,
            IsOpen = updateGlobalChatEvent.IsOpen
        };

        await _repository.Update(updateEntity, ct);

        return await GetAsync(updateGlobalChatEvent.Id);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid eventId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Delete, chatEvent);

        await _repository.Delete(eventId, ct);
    }

    /// <inheritdoc />
    public async Task<GlobalChatEvent> StartAsync(Guid eventId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Start, chatEvent);

        // The ordinary case, answered without a failing write; two starts that race past
        // this read meet the partial unique index over the live status.
        if (await _repository.HasActiveEvent())
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AnotherEventIsLive);
        }

        GlobalChatEvent? result;
        try
        {
            result = await _repository.UpdateStatus(
                eventId,
                GlobalChatEventStatus.Scheduled,
                GlobalChatEventStatus.Live,
                startedAt: _dateTimeProvider.Now,
                endedAt: null,
                ct);
        }
        catch (DuplicateEntityException)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AnotherEventIsLive);
        }

        if (result == null)
        {
            // Started or ended beside this call: the announcement belongs to that one.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.UnknownStatusTransition);
        }

        await _eventProducer.SendAsync(EventType.GlobalChatEventStarted, eventId);

        return result;
    }

    /// <inheritdoc />
    public async Task<GlobalChatEvent> EndAsync(Guid eventId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.End, chatEvent);

        var result = await _repository.UpdateStatus(
            eventId,
            GlobalChatEventStatus.Live,
            GlobalChatEventStatus.Ended,
            startedAt: null,
            endedAt: _dateTimeProvider.Now,
            ct);

        if (result == null)
        {
            // Ended beside this call, as with the start.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.UnknownStatusTransition);
        }

        await _eventProducer.SendAsync(EventType.GlobalChatEventEnded, eventId);

        return result;
    }

    /// <summary>
    /// One person joins an event, by his own request or by an organizer's. A person
    /// already on the list is a 409 rather than a silent success, whether the read or the
    /// unique pair finds him: the state asked for already holds. Only the join that wrote
    /// a row is announced, or the chat hears of it once per click. The refusal is a
    /// parameter because the two doors have different readers: the one who signed himself
    /// up, and the organizer who named somebody else.
    /// </summary>
    private async Task<GlobalChatEventParticipant> AddParticipant(
        Guid eventId, Guid userId, string alreadyOnTheList, CancellationToken ct)
    {
        if (await _repository.IsParticipant(eventId, userId))
        {
            throw new HttpException(HttpStatusCode.Conflict, alreadyOnTheList);
        }

        var participant = _factory.CreateParticipant(eventId, userId, isOrganizer: false);
        var result = await _repository.AddParticipant(participant, ct);
        if (result == null)
        {
            throw new HttpException(HttpStatusCode.Conflict, alreadyOnTheList);
        }

        await _eventProducer.SendAsync(EventType.GlobalChatEventParticipantJoined, eventId);

        return result;
    }

    /// <inheritdoc />
    public async Task<GlobalChatEventParticipant> JoinAsync(Guid eventId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Join, chatEvent);

        var userId = _identityProvider.Current.User.UserId;

        return await AddParticipant(
            eventId, userId, RefusalMessage.AlreadyParticipant, ct);
    }

    /// <inheritdoc />
    public async Task LeaveAsync(Guid eventId, CancellationToken ct = default) =>
        await RemoveParticipant(
            eventId, _identityProvider.Current.User.UserId, GlobalChatEventIntention.Leave, ct);

    /// <inheritdoc />
    public async Task<GlobalChatEventParticipant> AddParticipantAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var chatEvent = await GetAsync(eventId);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.AddParticipant, chatEvent);

        return await AddParticipant(
            eventId, userId, RefusalMessage.UserAlreadyParticipant, ct);
    }

    /// <inheritdoc />
    public async Task RemoveParticipantAsync(Guid eventId, Guid userId, CancellationToken ct = default) =>
        await RemoveParticipant(eventId, userId, GlobalChatEventIntention.RemoveParticipant, ct);

    private async Task RemoveParticipant(
        Guid eventId, Guid userId, GlobalChatEventIntention intention, CancellationToken ct)
    {
        var chatEvent = await GetAsync(eventId);
        _intentionManager.ThrowIfForbidden(intention, chatEvent);

        await _repository.RemoveParticipant(eventId, userId, ct);

        await _eventProducer.SendAsync(EventType.GlobalChatEventParticipantLeft, eventId);
    }
}
