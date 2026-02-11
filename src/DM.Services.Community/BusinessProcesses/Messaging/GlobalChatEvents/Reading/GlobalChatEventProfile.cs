using AutoMapper;
using DbGlobalChatEvent = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEvent;
using DbGlobalChatEventParticipant = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEventParticipant;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

/// <inheritdoc />
internal class GlobalChatEventProfile : Profile
{
    /// <inheritdoc />
    public GlobalChatEventProfile()
    {
        CreateMap<DbGlobalChatEvent, GlobalChatEvent>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.GlobalChatEventId))
            .ForMember(d => d.StartsAt, s => s.MapFrom(e => e.StartsAtUtc))
            .ForMember(d => d.CreatedAt, s => s.MapFrom(e => e.CreatedAtUtc))
            .ForMember(d => d.StartedAt, s => s.MapFrom(e => e.StartedAtUtc))
            .ForMember(d => d.EndedAt, s => s.MapFrom(e => e.EndedAtUtc));

        CreateMap<DbGlobalChatEventParticipant, GlobalChatEventParticipant>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.GlobalChatEventParticipantId))
            .ForMember(d => d.JoinedAt, s => s.MapFrom(p => p.JoinedAtUtc));
    }
}
