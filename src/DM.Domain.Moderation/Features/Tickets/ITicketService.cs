using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// DTO for creating a ticket
/// </summary>
public class CreateTicket
{
    /// <summary>
    /// Target username who is being reported
    /// </summary>
    public string TargetUsername { get; set; } = "";

    /// <summary>
    /// Entity ID that caused the report (comment, message, etc.)
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Type of entity (Comment, Message, Post, etc.)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Snapshot of the reported content
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Reporter's explanation of the issue
    /// </summary>
    public string Comment { get; set; } = "";
}

/// <summary>
/// DTO for creating a ticket from the public intake forms (/support and /complaint).
/// Unlike <see cref="CreateTicket"/>, guests are allowed to submit and the
/// target user is optional.
/// </summary>
public class CreateTicketIntake
{
    /// <summary>
    /// Ticket category
    /// </summary>
    public TicketSubtype Subtype { get; set; }

    /// <summary>
    /// Optional short subject line. The intake forms do not ask for one; the
    /// screens that read a ticket back name it by its category instead.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Ticket text in the site markup, the tag set of profile texts: neither
    /// [mod] nor [private]
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Contact for the reply (stored as guest email for unauthenticated authors)
    /// </summary>
    public string? Contact { get; set; }

    /// <summary>
    /// Optional link to the reported violation (complaints)
    /// </summary>
    public string? ViolationUrl { get; set; }

    /// <summary>
    /// Optional username of the user the complaint is about. Written into the text
    /// of the ticket as a line of its own, the way the link is
    /// </summary>
    public string? TargetUsername { get; set; }
}

/// <summary>
/// Service for moderation ticket operations
/// </summary>
public interface ITicketService
{
    /// <summary>
    /// Get the queue: tickets with optional status and subtype filters, the ones
    /// waiting for moderation first, then those waiting for their author, then
    /// the closed and the spam, each group newest first. Visible subtypes are
    /// derived from the caller role: Moderator sees user complaints and
    /// suggestions, SeniorModerator additionally sees complaints about junior
    /// moderator decisions, Admin sees everything. A complaint that names the
    /// caller is not in his queue (TicketVisibility).
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
    /// Get a ticket with its conversation by its public tracking token (guest
    /// tracking path, no authentication — the token is the credential). Returns
    /// null for an empty or unknown token
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
    /// Answer in the conversation of a ticket, as its author or as moderation, and
    /// read the ticket back as it stands after the answer. The author's answer
    /// leaves the ticket waiting for moderation. A moderator's answer moves it to
    /// the status he chose with it: waiting for the author unless he chose to
    /// close it or to mark it as spam, and for those two the text may stay empty,
    /// the change of the status being the whole answer. A ticket closed as
    /// resolved is announced to its author when the author has an account; one
    /// marked as spam is not
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
