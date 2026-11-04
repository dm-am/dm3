using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Tickets;

public interface ITicketService
{
    /// <summary>
    /// Get the queue of the caller (TicketVisibility), with optional status and subtype
    /// filters: waiting for moderation first, then waiting for the author, then closed
    /// and spam, each group newest first.
    /// </summary>
    Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetTickets(PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default);

    /// <summary>
    /// Get a page of tickets filed by the current user, with optional status and
    /// subtype filters (applied server-side)
    /// </summary>
    Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetMyFiledTickets(PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default);

    /// <summary>
    /// Get a ticket with its conversation: for its author, and for a moderator who
    /// moderates it (TicketVisibility). Anybody else is answered 404, the same as
    /// for a ticket that does not exist
    /// </summary>
    Task<TicketDetails> GetTicket(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Get a ticket with its conversation by its public tracking token, the token being
    /// the credential. Returns null for an empty or unknown token
    /// </summary>
    Task<TicketDetails?> GetTicketByTrackingToken(string token, CancellationToken ct = default);

    /// <summary>
    /// Create a new ticket (report)
    /// </summary>
    Task<Ticket> CreateTicket(CreateTicket createTicket, CancellationToken ct = default);

    /// <summary>
    /// Create a new ticket from the public intake forms.
    /// Guests are allowed; authenticated authors are recorded automatically
    /// </summary>
    Task<Ticket> CreateIntakeTicket(CreateTicketIntake createTicketIntake, CancellationToken ct = default);

    /// <summary>
    /// Answer in the conversation of a ticket and read it back as it stands after the
    /// answer. The author's answer leaves it waiting for moderation; a moderator's moves
    /// it to the status he chose, waiting for the author unless he closed it or marked it
    /// as spam, and for those two the text may stay empty. A ticket closed as resolved is
    /// announced to an author with an account; one marked as spam is not
    /// </summary>
    Task<TicketDetails> CreateComment(Guid ticketId, CreateTicketComment createComment,
        CancellationToken ct = default);

    /// <summary>
    /// Write in the conversation of a guest ticket by its tracking token: the
    /// comment of the guest who filed it, signed by nobody
    /// </summary>
    Task<TicketComment> CreateTrackedComment(string token, CreateTicketComment createComment,
        CancellationToken ct = default);

    /// <summary>
    /// Get ticket statistics: the counts of the caller's queue by status
    /// </summary>
    Task<Dictionary<TicketStatus, int>> GetTicketStats(CancellationToken ct = default);
}
