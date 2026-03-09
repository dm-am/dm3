using AutoMapper;
using DM.Domain.Moderation.Features.Tickets;
using DomainTicket = DM.Domain.Moderation.Features.Tickets.Ticket;

namespace DM.Web.API.Features.Moderation.Tickets;

/// <inheritdoc />
internal class TicketMappingProfile : Profile
{
    /// <inheritdoc />
    public TicketMappingProfile()
    {
        CreateMap<DomainTicket, Ticket>()
            .ForMember(d => d.ReporterLogin, s => s.MapFrom(t => t.ReporterUsername))
            .ForMember(d => d.TargetLogin, s => s.MapFrom(t => t.TargetUsername))
            .ForMember(d => d.AssignedModeratorLogin, s => s.MapFrom(t => t.AssignedModeratorUsername));
        CreateMap<CreateTicketRequest, CreateTicket>()
            .ForMember(d => d.TargetUsername, s => s.MapFrom(r => r.TargetLogin));
        CreateMap<ResolveTicketRequest, ResolveTicket>();
    }
}
