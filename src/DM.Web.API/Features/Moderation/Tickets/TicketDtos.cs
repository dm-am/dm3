using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Tickets;

/// <summary>
/// Moderation ticket DTO
/// </summary>
public class Ticket
{
    /// <summary>
    /// Ticket identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reporter username (null for guest submissions)
    /// </summary>
    public string? ReporterUsername { get; set; }

    /// <summary>
    /// Target user username (null for tickets without a target)
    /// </summary>
    public string? TargetUsername { get; set; }

    /// <summary>
    /// Contact email left by a guest author (null for authenticated submissions)
    /// </summary>
    public string? GuestEmail { get; set; }

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
    /// Ticket category
    /// </summary>
    public TicketSubtype Subtype { get; set; }

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
    public string? AssignedModeratorUsername { get; set; }

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
/// Moderation ticket detail DTO: the base ticket plus the conversation
/// thread. Returned by the detail endpoint only — list endpoints keep the
/// lighter Ticket payload. Answer stays for backward compatibility as the
/// single resolution response
/// </summary>
public class TicketDetails : Ticket
{
    /// <summary>
    /// Responses in the ticket conversation, oldest first
    /// </summary>
    public IEnumerable<TicketResponse> Responses { get; set; } = [];
}

/// <summary>
/// A single response in a ticket conversation
/// </summary>
public class TicketResponse
{
    /// <summary>
    /// Response author (lightweight reference)
    /// </summary>
    public UserRef Author { get; set; } = null!;

    /// <summary>
    /// Response text
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// When the response was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Whether this response is from a moderator (vs the user)
    /// </summary>
    public bool IsFromModerator { get; set; }
}

/// <summary>
/// Request to create a ticket
/// </summary>
public class CreateTicketRequest
{
    /// <summary>
    /// Target user username
    /// </summary>
    /// <example>problemuser</example>
    public string TargetUsername { get; set; } = "";

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
    /// Tickets waiting for a moderation response
    /// </summary>
    public int WaitingForModeration { get; set; }

    /// <summary>
    /// Tickets waiting for the user
    /// </summary>
    public int WaitingForUser { get; set; }

    /// <summary>
    /// Closed tickets count
    /// </summary>
    public int Closed { get; set; }

    /// <summary>
    /// Spam tickets count
    /// </summary>
    public int Spam { get; set; }
}
