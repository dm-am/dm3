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
            .ForMember(d => d.StartsUtc, s => s.MapFrom(e => e.StartsUtc))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(e => e.CreatedUtc));

        CreateMap<ServiceGlobalChatEvent, GlobalChatEventSummary>()
            .ForMember(d => d.StartsUtc, s => s.MapFrom(e => e.StartsUtc))
            .ForMember(d => d.ParticipantCount, s => s.MapFrom(e => e.Participants != null ? e.Participants.Count() : 0));

        CreateMap<ServiceGlobalChatEventParticipant, GlobalChatEventParticipant>()
            .ForMember(d => d.JoinedUtc, s => s.MapFrom(p => p.JoinedUtc));

        CreateMap<CreateGlobalChatEventInput, CreateGlobalChatEvent>()
            .ForMember(d => d.StartsUtc, s => s.MapFrom(i => i.StartsUtc));
    }
}
