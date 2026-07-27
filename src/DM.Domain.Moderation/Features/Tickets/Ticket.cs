using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
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
    /// Reporter user identifier (null for guest submissions).
    /// Server-side only — used to authorize the reporter to view their own
    /// ticket; not surfaced on the API-facing Ticket DTO.
    /// </summary>
    public Guid? ReporterUserId { get; set; }

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
    /// Public tracking token for guest submissions (null for authenticated
    /// authors). Returned once on intake so the guest can check their ticket.
    /// </summary>
    public string? TrackingToken { get; set; }

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
/// DTO for ticket detail output: the base ticket plus the conversation
/// thread. Loaded only on the detail path — list endpoints stay light
/// </summary>
public class TicketDetails : Ticket
{
    /// <summary>
    /// Responses in the ticket conversation, oldest first
    /// </summary>
    public IEnumerable<TicketResponseItem> Responses { get; set; } = [];
}

/// <summary>
/// DTO for a single response in a ticket conversation
/// </summary>
public class TicketResponseItem
{
    /// <summary>
    /// Response author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

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
/// Entity DTO for creating a ticket (repository level)
/// </summary>
public class CreateTicketEntity
{
    /// <summary>
    /// Ticket identifier
    /// </summary>
    public Guid TicketId { get; set; }

    /// <summary>
    /// Reporter user identifier (null for guest submissions)
    /// </summary>
    public Guid? ReporterUserId { get; set; }

    /// <summary>
    /// Target user identifier (null for tickets without a target)
    /// </summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>
    /// Contact email left by a guest author (null for authenticated submissions)
    /// </summary>
    public string? GuestEmail { get; set; }

    /// <summary>
    /// Public tracking token for guest submissions (null for authenticated authors)
    /// </summary>
    public string? TrackingToken { get; set; }

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
    /// Ticket category
    /// </summary>
    public TicketSubtype Subtype { get; set; }

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
