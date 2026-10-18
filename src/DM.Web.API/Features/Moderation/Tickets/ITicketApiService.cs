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
    /// Get the queue: tickets visible to the caller role, narrowed by status and
    /// subtype
    /// </summary>
    Task<ListEnvelope<Ticket>> GetTickets(PagingQuery query, TicketStatus? status = null,
        TicketSubtype? subtype = null);

    /// <summary>
    /// Get a page of tickets filed by current user, optionally filtered by status and subtype
    /// </summary>
    Task<ListEnvelope<Ticket>> GetMyFiledTickets(PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null);

    /// <summary>
    /// Get ticket by ID with the conversation
    /// </summary>
    Task<Envelope<TicketDetails>> GetTicket(Guid ticketId);

    /// <summary>
    /// Create a new ticket
    /// </summary>
    Task<Envelope<Ticket>> CreateTicket(CreateTicketRequest request);

    /// <summary>
    /// Answer in the conversation of a ticket, and read the ticket back as it
    /// stands after the answer
    /// </summary>
    Task<Envelope<TicketDetails>> CreateComment(Guid ticketId, AnswerTicketRequest request);

    /// <summary>
    /// Get ticket statistics
    /// </summary>
    Task<TicketStats> GetStats();
}
