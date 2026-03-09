using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Repository for moderation ticket operations
/// </summary>
public interface ITicketRepository
{
    /// <summary>
    /// Get all tickets with optional filtering
    /// </summary>
    Task<IEnumerable<Ticket>> GetTickets(TicketStatus? status = null, CancellationToken ct = default);

    /// <summary>
    /// Get tickets assigned to a specific moderator
    /// </summary>
    Task<IEnumerable<Ticket>> GetModeratorTickets(Guid moderatorId, CancellationToken ct = default);

    /// <summary>
    /// Get tickets filed by a specific user
    /// </summary>
    Task<IEnumerable<Ticket>> GetUserTickets(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get ticket by ID
    /// </summary>
    Task<Ticket?> Get(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Create a new ticket
    /// </summary>
    Task<Ticket> Create(CreateTicketEntity ticket, CancellationToken ct = default);

    /// <summary>
    /// Update ticket
    /// </summary>
    Task<Ticket> Update(UpdateTicketEntity ticket, CancellationToken ct = default);

    /// <summary>
    /// Get ticket count by status
    /// </summary>
    Task<Dictionary<TicketStatus, int>> GetTicketCounts(CancellationToken ct = default);
}
