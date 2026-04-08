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
        CreateMap<DomainTicket, Ticket>();
        CreateMap<CreateTicketRequest, CreateTicket>();
        CreateMap<ResolveTicketRequest, ResolveTicket>();
    }
}
