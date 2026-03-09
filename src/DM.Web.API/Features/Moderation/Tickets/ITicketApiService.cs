using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Tickets;

/// <summary>
/// API service for ticket management
/// </summary>
public interface ITicketApiService
{
    /// <summary>
    /// Get all tickets
    /// </summary>
    Task<ListEnvelope<Ticket>> GetTickets(TicketStatus? status = null);

    /// <summary>
    /// Get tickets assigned to current moderator
    /// </summary>
    Task<ListEnvelope<Ticket>> GetMyAssignedTickets();

    /// <summary>
    /// Get tickets filed by current user
    /// </summary>
    Task<ListEnvelope<Ticket>> GetMyFiledTickets();

    /// <summary>
    /// Get ticket by ID
    /// </summary>
    Task<Envelope<Ticket>?> GetTicket(Guid ticketId);

    /// <summary>
    /// Create a new ticket
    /// </summary>
    Task<Envelope<Ticket>> CreateTicket(CreateTicketRequest request);

    /// <summary>
    /// Assign ticket to current moderator
    /// </summary>
    Task<Envelope<Ticket>> AssignToMe(Guid ticketId);

    /// <summary>
    /// Resolve a ticket
    /// </summary>
    Task<Envelope<Ticket>> ResolveTicket(Guid ticketId, ResolveTicketRequest request);

    /// <summary>
    /// Get ticket statistics
    /// </summary>
    Task<TicketStats> GetStats();
}
