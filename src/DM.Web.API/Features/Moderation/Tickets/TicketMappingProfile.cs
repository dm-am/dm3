using AutoMapper;
using DM.Domain.Moderation.Features.Tickets;
using DomainTicket = DM.Domain.Moderation.Features.Tickets.Ticket;
using DomainTicketDetails = DM.Domain.Moderation.Features.Tickets.TicketDetails;

namespace DM.Web.API.Features.Moderation.Tickets;

/// <inheritdoc />
internal class TicketMappingProfile : Profile
{
    /// <inheritdoc />
    public TicketMappingProfile()
    {
        // Domain Ticket.TicketId → API Ticket.Id (field rename; otherwise Id
        // stays Guid.Empty and clients cannot address the ticket).
        CreateMap<DomainTicket, Ticket>()
            .ForMember(d => d.Id, s => s.MapFrom(t => t.TicketId));
        // Detail projection adds the conversation thread (Responses map by
        // convention, null-safe: an empty domain collection stays empty).
        CreateMap<DomainTicketDetails, TicketDetails>()
            .IncludeBase<DomainTicket, Ticket>();
        // Author (GeneralUser → UserRef) is covered by UserRefMappingProfile.
        CreateMap<TicketResponseItem, TicketResponse>();
        CreateMap<CreateTicketRequest, CreateTicket>();
        CreateMap<ResolveTicketRequest, ResolveTicket>();
    }
}
