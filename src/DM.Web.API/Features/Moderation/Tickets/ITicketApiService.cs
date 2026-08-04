using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Moderation.Tickets;

/// <summary>
/// API service for ticket management
/// </summary>
public interface ITicketApiService
{
    /// <summary>
    /// Get all tickets visible to the caller role
    /// </summary>
    Task<ListEnvelope<Ticket>> GetTickets(PagingQuery query, TicketStatus? status = null, TicketSubtype? subtype = null);

    /// <summary>
    /// Get tickets assigned to current moderator
    /// </summary>
    Task<ListEnvelope<Ticket>> GetMyAssignedTickets();

    /// <summary>
    /// Get tickets filed by current user, optionally filtered by status and subtype
    /// </summary>
    Task<ListEnvelope<Ticket>> GetMyFiledTickets(TicketStatus? status = null, TicketSubtype? subtype = null);

    /// <summary>
    /// Get ticket by ID with the conversation thread
    /// </summary>
    Task<Envelope<TicketDetails>?> GetTicket(Guid ticketId);

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
