using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Repository for moderation ticket operations
/// </summary>
public interface ITicketRepository
{
    /// <summary>
    /// Get all tickets with optional filtering.
    /// A null <paramref name="subtypes"/> means no subtype filtering
    /// </summary>
    Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetTickets(PagingQuery query,
        TicketStatus? status = null,
        IReadOnlyCollection<TicketSubtype>? subtypes = null, CancellationToken ct = default);

    /// <summary>
    /// Get a page of tickets assigned to a specific moderator
    /// </summary>
    Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetModeratorTickets(Guid moderatorId,
        PagingQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get a page of tickets filed by a specific user, with optional status and
    /// subtype filters (applied server-side)
    /// </summary>
    Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetUserTickets(Guid userId, PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default);

    /// <summary>
    /// Get ticket by ID
    /// </summary>
    Task<Ticket?> Get(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Get ticket by ID with the conversation thread (detail path only)
    /// </summary>
    Task<TicketDetails?> GetDetails(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Get a ticket with its conversation thread by its public tracking token
    /// (guest tracking path). Returns null for an empty or unknown token
    /// </summary>
    Task<TicketDetails?> GetByTrackingToken(string token, CancellationToken ct = default);

    /// <summary>
    /// Create a new ticket
    /// </summary>
    Task<Ticket> Create(CreateTicketEntity ticket, CancellationToken ct = default);

    /// <summary>
    /// Update ticket
    /// </summary>
    Task<Ticket> Update(UpdateTicketEntity ticket, CancellationToken ct = default);

    /// <summary>
    /// Get ticket count by status.
    /// A null <paramref name="subtypes"/> means no subtype filtering
    /// </summary>
    Task<Dictionary<TicketStatus, int>> GetTicketCounts(
        IReadOnlyCollection<TicketSubtype>? subtypes = null, CancellationToken ct = default);
}
