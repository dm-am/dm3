using System.Linq;
using AutoMapper;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using ServiceGlobalChatEvent = DM.Domain.Messaging.Features.GlobalChatEvents.GlobalChatEvent;
using ServiceGlobalChatEventParticipant = DM.Domain.Messaging.Features.GlobalChatEvents.GlobalChatEventParticipant;

namespace DM.Web.API.Features.Messaging.GlobalChatEvents;

/// <inheritdoc />
internal class GlobalChatEventMappingProfile : Profile
{
    /// <inheritdoc />
    public GlobalChatEventMappingProfile()
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
