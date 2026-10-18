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
    /// Get a page of the queue with optional filtering, in the order of the
    /// statuses (waiting for moderation, waiting for the user, closed, spam) and
    /// newest first within each.
    /// A null <paramref name="subtypes"/> means no subtype filtering. The tickets
    /// that name <paramref name="exceptTargetUserId"/> as the violator are left
    /// out: the queue of a moderator holds what he moderates (TicketVisibility)
    /// </summary>
    Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetTickets(PagingQuery query,
        TicketStatus? status = null,
        IReadOnlyCollection<TicketSubtype>? subtypes = null, Guid? exceptTargetUserId = null,
        CancellationToken ct = default);

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
    /// Get a ticket by its public tracking token (guest tracking path).
    /// Returns null for an empty or unknown token
    /// </summary>
    Task<Ticket?> GetByTrackingToken(string token, CancellationToken ct = default);

    /// <summary>
    /// The conversation of a ticket, from the oldest comment to the newest
    /// </summary>
    Task<IReadOnlyList<TicketComment>> GetComments(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Create a new ticket
    /// </summary>
    Task<Ticket> Create(CreateTicketEntity ticket, CancellationToken ct = default);

    /// <summary>
    /// Add a comment to the conversation of an open ticket and move the ticket to
    /// <paramref name="statusAfter"/>, both or neither. A status that ends the
    /// ticket (closed, spam) closes it in the name of the comment's author, as
    /// <see cref="Close" /> does. Nothing is written into a ticket closed by the
    /// time the comment arrives, nor into a conversation that already holds
    /// <paramref name="maxComments"/> comments; the comment as it is read back
    /// comes with <see cref="TicketCommentOutcome.Added" /> only
    /// </summary>
    Task<(TicketCommentOutcome outcome, TicketComment? comment)> AddComment(
        CreateTicketCommentEntity comment, TicketStatus statusAfter, int maxComments,
        CancellationToken ct = default);

    /// <summary>
    /// Close an open ticket with the given status, naming who closed it and when.
    /// False when the ticket was not open any more: of two closings, the second
    /// one closes nothing
    /// </summary>
    Task<bool> Close(Guid ticketId, TicketStatus status, Guid resolvedById, DateTimeOffset resolvedUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Get ticket count by status.
    /// A null <paramref name="subtypes"/> means no subtype filtering; the tickets
    /// that name <paramref name="exceptTargetUserId"/> as the violator are not
    /// counted, as they are not listed by <see cref="GetTickets" />
    /// </summary>
    Task<Dictionary<TicketStatus, int>> GetTicketCounts(
        IReadOnlyCollection<TicketSubtype>? subtypes = null, Guid? exceptTargetUserId = null,
        CancellationToken ct = default);
}
