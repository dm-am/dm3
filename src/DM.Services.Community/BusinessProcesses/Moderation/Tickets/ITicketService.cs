using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Moderation.Tickets;

/// <summary>
/// DTO for creating a ticket
/// </summary>
public class CreateTicket
{
    /// <summary>
    /// Target user login who is being reported
    /// </summary>
    public string TargetLogin { get; set; } = "";

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
/// DTO for ticket output
/// </summary>
public class TicketDto
{
    /// <summary>
    /// Ticket ID
    /// </summary>
    public Guid TicketId { get; set; }

    /// <summary>
    /// Reporter login
    /// </summary>
    public string ReporterLogin { get; set; } = "";

    /// <summary>
    /// Target user login
    /// </summary>
    public string TargetLogin { get; set; } = "";

    /// <summary>
    /// Entity ID
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Entity type
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Ticket status
    /// </summary>
    public TicketStatus Status { get; set; }

    /// <summary>
    /// Creation time
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Reported content snapshot
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Reporter's comment
    /// </summary>
    public string Comment { get; set; } = "";

    /// <summary>
    /// Assigned moderator login
    /// </summary>
    public string? AssignedModeratorLogin { get; set; }

    /// <summary>
    /// Resolution time
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Moderator's answer
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    /// Warning was issued
    /// </summary>
    public bool HasWarning { get; set; }

    /// <summary>
    /// Ban was issued
    /// </summary>
    public bool HasBan { get; set; }
}

/// <summary>
/// Service for moderation ticket operations
/// </summary>
public interface ITicketService
{
    /// <summary>
    /// Get all tickets with optional status filter
    /// </summary>
    Task<IEnumerable<TicketDto>> GetTickets(TicketStatus? status = null, CancellationToken ct = default);

    /// <summary>
    /// Get tickets for the current moderator
    /// </summary>
    Task<IEnumerable<TicketDto>> GetMyAssignedTickets(CancellationToken ct = default);

    /// <summary>
    /// Get tickets filed by the current user
    /// </summary>
    Task<IEnumerable<TicketDto>> GetMyFiledTickets(CancellationToken ct = default);

    /// <summary>
    /// Get ticket by ID
    /// </summary>
    Task<TicketDto?> GetTicket(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Create a new ticket (report)
    /// </summary>
    Task<TicketDto> CreateTicket(CreateTicket createTicket, CancellationToken ct = default);

    /// <summary>
    /// Assign a ticket to the current moderator
    /// </summary>
    Task<TicketDto> AssignToMe(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Resolve a ticket
    /// </summary>
    Task<TicketDto> ResolveTicket(Guid ticketId, ResolveTicket resolveTicket, CancellationToken ct = default);

    /// <summary>
    /// Get ticket statistics
    /// </summary>
    Task<Dictionary<TicketStatus, int>> GetTicketStats(CancellationToken ct = default);
}
