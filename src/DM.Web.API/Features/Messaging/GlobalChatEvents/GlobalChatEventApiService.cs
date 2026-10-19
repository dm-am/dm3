using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Domain.Personal.Features.Profiles;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Messaging.GlobalChatEvents;

public class GlobalChatEventApiService
{
    private readonly IGlobalChatEventService _eventService;
    private readonly IUserService _userService;
    private readonly GlobalChatEventMapper _mapper;

    public GlobalChatEventApiService(
        IGlobalChatEventService eventService,
        IUserService userService,
        GlobalChatEventMapper mapper)
    {
        _eventService = eventService;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<GlobalChatEventSummary>> GetList()
    {
        var events = await _eventService.GetAllAsync();
        var summaries = events.Select(_mapper.ToSummary);
        return new ListEnvelope<GlobalChatEventSummary>(summaries);
    }

    public async Task<Envelope<GlobalChatEvent>> Get(Guid id)
    {
        var chatEvent = await _eventService.GetAsync(id);
        return new Envelope<GlobalChatEvent>(_mapper.ToEvent(chatEvent));
    }

    public async Task<Envelope<GlobalChatEventSummary>?> GetActive()
    {
        var chatEvent = await _eventService.GetActiveEventAsync();
        if (chatEvent == null)
        {
            return null;
        }
        return new Envelope<GlobalChatEventSummary>(_mapper.ToSummary(chatEvent));
    }

    public async Task<Envelope<GlobalChatEvent>> Create(CreateGlobalChatEventInput input)
    {
        var chatEvent = await _eventService.CreateAsync(_mapper.ToCreateEvent(input));
        return new Envelope<GlobalChatEvent>(_mapper.ToEvent(chatEvent));
    }

    public async Task<Envelope<GlobalChatEvent>> Update(Guid id, UpdateGlobalChatEventInput input)
    {
        var updateDto = _mapper.ToUpdateEvent(input);
        updateDto.Id = id;
        var chatEvent = await _eventService.UpdateAsync(updateDto);
        return new Envelope<GlobalChatEvent>(_mapper.ToEvent(chatEvent));
    }

    public Task Delete(Guid id) => _eventService.DeleteAsync(id);

    public async Task<Envelope<GlobalChatEvent>> Start(Guid id)
    {
        var chatEvent = await _eventService.StartAsync(id);
        return new Envelope<GlobalChatEvent>(_mapper.ToEvent(chatEvent));
    }

    public async Task<Envelope<GlobalChatEvent>> End(Guid id)
    {
        var chatEvent = await _eventService.EndAsync(id);
        return new Envelope<GlobalChatEvent>(_mapper.ToEvent(chatEvent));
    }

    public async Task<Envelope<GlobalChatEvent>> Join(Guid id)
    {
        await _eventService.JoinAsync(id);
        var chatEvent = await _eventService.GetAsync(id);
        return new Envelope<GlobalChatEvent>(_mapper.ToEvent(chatEvent));
    }

    public async Task<Envelope<GlobalChatEvent>> Leave(Guid id)
    {
        await _eventService.LeaveAsync(id);
        var chatEvent = await _eventService.GetAsync(id);
        return new Envelope<GlobalChatEvent>(_mapper.ToEvent(chatEvent));
    }

    public async Task<Envelope<GlobalChatEvent>> AddParticipant(Guid id, AddParticipantInput input)
    {
        var user = await _userService.GetAsync(input.Username);

        await _eventService.AddParticipantAsync(id, user.UserId);
        var chatEvent = await _eventService.GetAsync(id);
        return new Envelope<GlobalChatEvent>(_mapper.ToEvent(chatEvent));
    }

    public async Task RemoveParticipant(Guid eventId, string login)
    {
        var user = await _userService.GetAsync(login);
        await _eventService.RemoveParticipantAsync(eventId, user.UserId);
    }
}
