using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// DTO for ticket output
/// </summary>
public class Ticket
{
    /// <summary>
    /// Ticket ID
    /// </summary>
    public Guid TicketId { get; set; }

    /// <summary>
    /// Reporter username
    /// </summary>
    public string ReporterUsername { get; set; } = "";

    /// <summary>
    /// Target user username
    /// </summary>
    public string TargetUsername { get; set; } = "";

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
    /// Assigned moderator username
    /// </summary>
    public string? AssignedModeratorUsername { get; set; }

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
/// Entity DTO for creating a ticket (repository level)
/// </summary>
public class CreateTicketEntity
{
    /// <summary>
    /// Ticket identifier
    /// </summary>
    public Guid TicketId { get; set; }

    /// <summary>
    /// Reporter user identifier
    /// </summary>
    public Guid ReporterUserId { get; set; }

    /// <summary>
    /// Target user identifier
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Entity identifier
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
    /// Creation timestamp
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
}

/// <summary>
/// Entity DTO for updating a ticket (repository level)
/// </summary>
public class UpdateTicketEntity
{
    /// <summary>
    /// Ticket identifier
    /// </summary>
    public Guid TicketId { get; set; }

    /// <summary>
    /// Assigned moderator identifier
    /// </summary>
    public Guid? AssignedModeratorId { get; set; }

    /// <summary>
    /// Ticket status
    /// </summary>
    public TicketStatus? Status { get; set; }

    /// <summary>
    /// Resolution timestamp
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Moderator's answer
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    /// Warning ID if one was issued
    /// </summary>
    public Guid? WarningId { get; set; }

    /// <summary>
    /// Ban ID if one was issued
    /// </summary>
    public Guid? BanId { get; set; }
}
