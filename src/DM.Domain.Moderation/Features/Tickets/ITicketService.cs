using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    /// Short subject line
    /// </summary>
    public string Subject { get; set; } = "";

    /// <summary>
    /// Ticket text (raw BBCode)
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
    /// Optional username of the user the complaint is about
    /// </summary>
    public string? TargetUsername { get; set; }
}

/// <summary>
/// DTO for resolving a ticket
/// </summary>
public class ResolveTicket
{
    /// <summary>
    /// Resolution status
    /// </summary>
    public TicketStatus Status { get; set; }

    /// <summary>
    /// Moderator's response
    /// </summary>
    public string Answer { get; set; } = "";

    /// <summary>
    /// Issue a warning
    /// </summary>
    public bool IssueWarning { get; set; }

    /// <summary>
    /// Warning message (if issuing warning)
    /// </summary>
    public string? WarningText { get; set; }

    /// <summary>
    /// Warning points (if issuing warning)
    /// </summary>
    public int WarningPoints { get; set; }

    /// <summary>
    /// Issue a ban
    /// </summary>
    public bool IssueBan { get; set; }

    /// <summary>
    /// Ban duration in hours (if issuing ban)
    /// </summary>
    public int? BanDurationHours { get; set; }

    /// <summary>
    /// Ban comment (if issuing ban)
    /// </summary>
    public string? BanComment { get; set; }
}

/// <summary>
/// Service for moderation ticket operations
/// </summary>
public interface ITicketService
{
    /// <summary>
    /// Get tickets with optional status and subtype filters.
    /// Visible subtypes are derived from the caller role: Moderator sees user
    /// complaints and suggestions, SeniorModerator additionally sees complaints
    /// about junior moderator decisions, Admin sees everything.
    /// </summary>
    Task<IEnumerable<Ticket>> GetTickets(TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default);

    /// <summary>
    /// Get tickets for the current moderator
    /// </summary>
    Task<IEnumerable<Ticket>> GetMyAssignedTickets(CancellationToken ct = default);

    /// <summary>
    /// Get tickets filed by the current user, with optional status and subtype
    /// filters (applied server-side)
    /// </summary>
    Task<IEnumerable<Ticket>> GetMyFiledTickets(
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default);

    /// <summary>
    /// Get ticket by ID with the conversation thread
    /// </summary>
    Task<TicketDetails?> GetTicket(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Get a ticket with its conversation thread by its public tracking token
    /// (guest tracking path, no authentication — the token is the credential).
    /// Returns null for an empty or unknown token
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
    /// Assign a ticket to the current moderator
    /// </summary>
    Task<Ticket> AssignToMe(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Resolve a ticket
    /// </summary>
    Task<Ticket> ResolveTicket(Guid ticketId, ResolveTicket resolveTicket, CancellationToken ct = default);

    /// <summary>
    /// Get ticket statistics
    /// </summary>
    Task<Dictionary<TicketStatus, int>> GetTicketStats(CancellationToken ct = default);
}
