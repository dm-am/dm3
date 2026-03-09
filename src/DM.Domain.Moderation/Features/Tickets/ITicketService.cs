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
    /// Get all tickets with optional status filter
    /// </summary>
    Task<IEnumerable<Ticket>> GetTickets(TicketStatus? status = null, CancellationToken ct = default);

    /// <summary>
    /// Get tickets for the current moderator
    /// </summary>
    Task<IEnumerable<Ticket>> GetMyAssignedTickets(CancellationToken ct = default);

    /// <summary>
    /// Get tickets filed by the current user
    /// </summary>
    Task<IEnumerable<Ticket>> GetMyFiledTickets(CancellationToken ct = default);

    /// <summary>
    /// Get ticket by ID
    /// </summary>
    Task<Ticket?> GetTicket(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Create a new ticket (report)
    /// </summary>
    Task<Ticket> CreateTicket(CreateTicket createTicket, CancellationToken ct = default);

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
