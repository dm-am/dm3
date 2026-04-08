using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Domain.Personal.Features.Profiles;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Messaging.GlobalChatEvents;

/// <inheritdoc />
internal class GlobalChatEventApiService : IGlobalChatEventApiService
{
    private readonly IGlobalChatEventService _eventService;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GlobalChatEventApiService(
        IGlobalChatEventService eventService,
        IUserService userService,
        IMapper mapper)
    {
        _eventService = eventService;
        _userService = userService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<GlobalChatEventSummary>> GetList(CancellationToken ct = default)
    {
        var events = await _eventService.GetAllAsync();
        var summaries = events.Select(_mapper.Map<GlobalChatEventSummary>);
        return new ListEnvelope<GlobalChatEventSummary>(summaries);
    }

    /// <inheritdoc />
    public async Task<Envelope<GlobalChatEvent>> Get(Guid id, CancellationToken ct = default)
    {
        var chatEvent = await _eventService.GetAsync(id);
        return new Envelope<GlobalChatEvent>(_mapper.Map<GlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<GlobalChatEventSummary>?> GetActive(CancellationToken ct = default)
    {
        var chatEvent = await _eventService.GetActiveEventAsync();
        if (chatEvent == null)
        {
            return null;
        }
        return new Envelope<GlobalChatEventSummary>(_mapper.Map<GlobalChatEventSummary>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<GlobalChatEvent>> Create(CreateGlobalChatEventInput input, CancellationToken ct = default)
    {
        var createDto = _mapper.Map<CreateGlobalChatEvent>(input);
        var chatEvent = await _eventService.CreateAsync(createDto, ct);
        return new Envelope<GlobalChatEvent>(_mapper.Map<GlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<GlobalChatEvent>> Update(Guid id, UpdateGlobalChatEventInput input, CancellationToken ct = default)
    {
        var updateDto = new UpdateGlobalChatEvent
        {
            Id = id,
            Title = input.Title,
            Description = input.Description,
            StartsUtc = input.StartsUtc,
            Duration = input.Duration,
            IsOpen = input.IsOpen
        };
        var chatEvent = await _eventService.UpdateAsync(updateDto, ct);
        return new Envelope<GlobalChatEvent>(_mapper.Map<GlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task Delete(Guid id, CancellationToken ct = default)
    {
        await _eventService.DeleteAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<Envelope<GlobalChatEvent>> Start(Guid id, CancellationToken ct = default)
    {
        var chatEvent = await _eventService.StartAsync(id, ct);
        return new Envelope<GlobalChatEvent>(_mapper.Map<GlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<GlobalChatEvent>> End(Guid id, CancellationToken ct = default)
    {
        var chatEvent = await _eventService.EndAsync(id, ct);
        return new Envelope<GlobalChatEvent>(_mapper.Map<GlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<GlobalChatEvent>> Join(Guid id, CancellationToken ct = default)
    {
        await _eventService.JoinAsync(id, ct);
        var chatEvent = await _eventService.GetAsync(id);
        return new Envelope<GlobalChatEvent>(_mapper.Map<GlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<GlobalChatEvent>> Leave(Guid id, CancellationToken ct = default)
    {
        await _eventService.LeaveAsync(id, ct);
        var chatEvent = await _eventService.GetAsync(id);
        return new Envelope<GlobalChatEvent>(_mapper.Map<GlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task<Envelope<GlobalChatEvent>> AddParticipant(Guid id, AddParticipantInput input, CancellationToken ct = default)
    {
        // Resolve user by username
        var user = await _userService.GetAsync(input.Username);

        await _eventService.AddParticipantAsync(id, user.UserId, ct);
        var chatEvent = await _eventService.GetAsync(id);
        return new Envelope<GlobalChatEvent>(_mapper.Map<GlobalChatEvent>(chatEvent));
    }

    /// <inheritdoc />
    public async Task RemoveParticipant(Guid eventId, string login, CancellationToken ct = default)
    {
        var user = await _userService.GetAsync(login);
        await _eventService.RemoveParticipantAsync(eventId, user.UserId, ct);
    }
}
