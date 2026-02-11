using System.Linq;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Creating;
using ServiceGlobalChatEvent = DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading.GlobalChatEvent;
using ServiceGlobalChatEventParticipant = DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading.GlobalChatEventParticipant;

namespace DM.Web.API.Dto.Messaging;

/// <inheritdoc />
internal class GlobalChatEventProfile : Profile
{
    /// <inheritdoc />
    public GlobalChatEventProfile()
    {
        CreateMap<ServiceGlobalChatEvent, GlobalChatEvent>()
            .ForMember(d => d.StartsAt, s => s.MapFrom(e => e.StartsAt))
            .ForMember(d => d.CreatedAt, s => s.MapFrom(e => e.CreatedAt));

        CreateMap<ServiceGlobalChatEvent, GlobalChatEventSummary>()
            .ForMember(d => d.StartsAt, s => s.MapFrom(e => e.StartsAt))
            .ForMember(d => d.ParticipantCount, s => s.MapFrom(e => e.Participants != null ? e.Participants.Count() : 0));

        CreateMap<ServiceGlobalChatEventParticipant, GlobalChatEventParticipant>()
            .ForMember(d => d.JoinedAt, s => s.MapFrom(p => p.JoinedAt));

        CreateMap<CreateGlobalChatEventInput, CreateGlobalChatEvent>()
            .ForMember(d => d.StartsAt, s => s.MapFrom(i => i.StartsAt));
    }
}
