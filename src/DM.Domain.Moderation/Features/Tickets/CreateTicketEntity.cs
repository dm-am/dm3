using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Entity DTO for creating a ticket (repository level)
/// </summary>
public class CreateTicketEntity
{
    public Guid TicketId { get; set; }

    /// <summary>
    /// Reporter user identifier, null for a guest submission
    /// </summary>
    public Guid? ReporterUserId { get; set; }

    public Guid? TargetUserId { get; set; }

    /// <summary>
    /// Contact email left by a guest author, null for an authenticated submission
    /// </summary>
    public string? GuestEmail { get; set; }

    /// <summary>
    /// Public tracking token of a guest submission, null for an authenticated author
    /// </summary>
    public string? TrackingToken { get; set; }

    public Guid? EntityId { get; set; }

    public string? EntityType { get; set; }

    public TicketStatus Status { get; set; }

    public TicketSubtype Subtype { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public string Description { get; set; } = "";

    public string Comment { get; set; } = "";
}
