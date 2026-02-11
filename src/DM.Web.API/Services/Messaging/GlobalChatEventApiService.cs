using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Creating;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Deleting;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Lifecycle;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Participants;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Updating;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Web.API.Dto.Contracts;
using ApiGlobalChatEvent = DM.Web.API.Dto.Messaging.GlobalChatEvent;
using ApiGlobalChatEventSummary = DM.Web.API.Dto.Messaging.GlobalChatEventSummary;
using ApiCreateGlobalChatEventInput = DM.Web.API.Dto.Messaging.CreateGlobalChatEventInput;
using ApiUpdateGlobalChatEventInput = DM.Web.API.Dto.Messaging.UpdateGlobalChatEventInput;
using ApiAddParticipantInput = DM.Web.API.Dto.Messaging.AddParticipantInput;

namespace DM.Web.API.Services.Messaging;

/// <inheritdoc />
internal class GlobalChatEventApiService : IGlobalChatEventApiService
{
    private readonly IGlobalChatEventReadingService _readingService;
    private readonly IGlobalChatEventCreatingService _creatingService;
    private readonly IGlobalChatEventUpdatingService _updatingService;
    private readonly IGlobalChatEventDeletingService _deletingService;
    private readonly IGlobalChatEventLifecycleService _lifecycleService;
    private readonly IGlobalChatEventParticipantService _participantService;
    private readonly IUserReadingService _userReadingService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GlobalChatEventApiService(
        IGlobalChatEventReadingService readingService,
        IGlobalChatEventCreatingService creatingService,
        IGlobalChatEventUpdatingService updatingService,
        IGlobalChatEventDeletingService deletingService,
        IGlobalChatEventLifecycleService lifecycleService,
        IGlobalChatEventParticipantService participantService,
        IUserReadingService userReadingService,
        IMapper mapper)
    {
        _readingService = readingService;
        _creatingService = creatingService;
        _updatingService = updatingService;
        _deletingService = deletingService;
        _lifecycleService = lifecycleService;
        _participantService = participantService;
        _userReadingService = userReadingService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ApiGlobalChatEventSummary>> GetList(CancellationToken ct = default)
    {
        var events = await _readingService.GetAll();
        var summaries = events.Select(_mapper.Map<ApiGlobalChatEventSummary>);
        return new ListEnvelope<ApiGlobalChatEventSummary>(summaries);
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiGlobalChatEvent>> Get(Guid id, CancellationToken ct = default)
    {
        var chatEvent = await _readingService.Get(id);
        return new Envelope<ApiGlobalChatEvent>(_mapper.Map<ApiGlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiGlobalChatEventSummary>?> GetActive(CancellationToken ct = default)
    {
        var chatEvent = await _readingService.GetActiveEvent();
        if (chatEvent == null)
        {
            return null;
        }
        return new Envelope<ApiGlobalChatEventSummary>(_mapper.Map<ApiGlobalChatEventSummary>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiGlobalChatEvent>> Create(ApiCreateGlobalChatEventInput input, CancellationToken ct = default)
    {
        var createDto = _mapper.Map<CreateGlobalChatEvent>(input);
        var chatEvent = await _creatingService.Create(createDto, ct);
        return new Envelope<ApiGlobalChatEvent>(_mapper.Map<ApiGlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiGlobalChatEvent>> Update(Guid id, ApiUpdateGlobalChatEventInput input, CancellationToken ct = default)
    {
        var updateDto = new UpdateGlobalChatEvent
        {
            Id = id,
            Title = input.Title,
            Description = input.Description,
            StartsAt = input.StartsAt,
            Duration = input.Duration,
            IsOpen = input.IsOpen
        };
        var chatEvent = await _updatingService.Update(updateDto, ct);
        return new Envelope<ApiGlobalChatEvent>(_mapper.Map<ApiGlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task Delete(Guid id, CancellationToken ct = default)
    {
        await _deletingService.Delete(id, ct);
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiGlobalChatEvent>> Start(Guid id, CancellationToken ct = default)
    {
        var chatEvent = await _lifecycleService.Start(id, ct);
        return new Envelope<ApiGlobalChatEvent>(_mapper.Map<ApiGlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiGlobalChatEvent>> End(Guid id, CancellationToken ct = default)
    {
        var chatEvent = await _lifecycleService.End(id, ct);
        return new Envelope<ApiGlobalChatEvent>(_mapper.Map<ApiGlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiGlobalChatEvent>> Join(Guid id, CancellationToken ct = default)
    {
        await _participantService.Join(id, ct);
        var chatEvent = await _readingService.Get(id);
        return new Envelope<ApiGlobalChatEvent>(_mapper.Map<ApiGlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiGlobalChatEvent>> Leave(Guid id, CancellationToken ct = default)
    {
        await _participantService.Leave(id, ct);
        var chatEvent = await _readingService.Get(id);
        return new Envelope<ApiGlobalChatEvent>(_mapper.Map<ApiGlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<ApiGlobalChatEvent>> AddParticipant(Guid id, ApiAddParticipantInput input, CancellationToken ct = default)
    {
        // Resolve user by login
        var user = await _userReadingService.Get(input.Login);

        await _participantService.AddParticipant(id, user.UserId, ct);
        var chatEvent = await _readingService.Get(id);
        return new Envelope<ApiGlobalChatEvent>(_mapper.Map<ApiGlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task RemoveParticipant(Guid eventId, string login, CancellationToken ct = default)
    {
        var user = await _userReadingService.Get(login);
        await _participantService.RemoveParticipant(eventId, user.UserId, ct);
    }
}
