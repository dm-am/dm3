using DM.Domain.Moderation.Features.Tickets;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using DomainTicket = DM.Domain.Moderation.Features.Tickets.Ticket;
using DomainTicketDetails = DM.Domain.Moderation.Features.Tickets.TicketDetails;

namespace DM.Web.API.Features.Moderation.Tickets;

/// <summary>
/// Compile-time mapper for moderation tickets. The author renders as UserRef
/// through the shared static mapper.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(UserRefMappers))]
internal partial class TicketMapper
{
    /// <summary>
    /// Domain ticket to the list row. TicketId becomes Id - otherwise Id
    /// stays Guid.Empty and clients cannot address the ticket.
    /// </summary>
    [MapProperty(nameof(DomainTicket.TicketId), nameof(Ticket.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial Ticket ToTicket(DomainTicket ticket);

    /// <summary>
    /// Domain detail projection to the detail DTO: the list row plus the
    /// conversation thread
    /// </summary>
    [MapProperty(nameof(DomainTicketDetails.TicketId), nameof(TicketDetails.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial TicketDetails ToTicketDetails(DomainTicketDetails ticket);

    /// <summary>
    /// Create request to the domain command
    /// </summary>
    public partial CreateTicket ToCreateTicket(CreateTicketRequest request);

    /// <summary>
    /// Resolve request to the domain command
    /// </summary>
    public partial ResolveTicket ToResolveTicket(ResolveTicketRequest request);
}
