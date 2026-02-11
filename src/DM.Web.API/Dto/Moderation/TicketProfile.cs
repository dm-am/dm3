using AutoMapper;
using DM.Services.Community.BusinessProcesses.Moderation.Tickets;

namespace DM.Web.API.Dto.Moderation;

/// <inheritdoc />
internal class TicketProfile : Profile
{
    /// <inheritdoc />
    public TicketProfile()
    {
        CreateMap<TicketDto, Ticket>();
        CreateMap<CreateTicketRequest, CreateTicket>();
        CreateMap<ResolveTicketRequest, ResolveTicket>();
    }
}
