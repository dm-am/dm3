using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Moderation;

/// <summary>
/// Moderation ticket DTO
/// </summary>
public class Ticket
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
    /// Entity ID that was reported
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
    /// Assigned moderator
    /// </summary>
    public string? AssignedModeratorLogin { get; set; }

    /// <summary>
    /// Resolution time
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Moderator's response
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
/// Request to create a ticket
/// </summary>
public class CreateTicketRequest
{
    /// <summary>
    /// Target user login
    /// </summary>
    /// <example>problemuser</example>
    public string TargetLogin { get; set; } = "";

    /// <summary>
    /// Entity ID that caused the report
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Entity type (Comment, Message, Post, etc.)
    /// </summary>
    /// <example>Comment</example>
    public string? EntityType { get; set; }

    /// <summary>
    /// Snapshot of the reported content
    /// </summary>
    /// <example>Offensive comment text here...</example>
    public string Description { get; set; } = "";

    /// <summary>
    /// Reporter's explanation
    /// </summary>
    /// <example>This comment violates the rules because...</example>
    public string Comment { get; set; } = "";
}

/// <summary>
/// Request to resolve a ticket
/// </summary>
public class ResolveTicketRequest
{
    /// <summary>
    /// Resolution status
    /// </summary>
    public TicketStatus Status { get; set; }

    /// <summary>
    /// Moderator's response
    /// </summary>
    /// <example>Action taken: warning issued for rule violation</example>
    public string Answer { get; set; } = "";

    /// <summary>
    /// Issue a warning
    /// </summary>
    public bool IssueWarning { get; set; }

    /// <summary>
    /// Warning message
    /// </summary>
    public string? WarningText { get; set; }

    /// <summary>
    /// Warning points
    /// </summary>
    public int WarningPoints { get; set; }

    /// <summary>
    /// Issue a ban
    /// </summary>
    public bool IssueBan { get; set; }

    /// <summary>
    /// Ban duration in hours
    /// </summary>
    public int? BanDurationHours { get; set; }

    /// <summary>
    /// Ban comment
    /// </summary>
    public string? BanComment { get; set; }
}

/// <summary>
/// Ticket statistics
/// </summary>
public class TicketStats
{
    /// <summary>
    /// Open tickets count
    /// </summary>
    public int Open { get; set; }

    /// <summary>
    /// In-progress tickets count
    /// </summary>
    public int InProgress { get; set; }

    /// <summary>
    /// Resolved tickets count
    /// </summary>
    public int Resolved { get; set; }

    /// <summary>
    /// Rejected tickets count
    /// </summary>
    public int Rejected { get; set; }
}
