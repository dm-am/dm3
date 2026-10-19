using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Tickets;

public class Ticket
{
    public Guid TicketId { get; set; }

    /// <summary>
    /// Reporter user identifier, null for a guest submission. Server-side only: it lets
    /// the reporter read his own ticket.
    /// </summary>
    public Guid? ReporterUserId { get; set; }

    /// <summary>
    /// The reporter, null for a guest submission and for a removed account alike:
    /// <see cref="IsGuest" /> tells the two apart
    /// </summary>
    public GeneralUser? Reporter { get; set; }

    /// <summary>
    /// Whether the ticket was filed without an account. Read off the ticket and
    /// not off <see cref="Reporter" />, which is also missing for the ticket of an
    /// account removed since
    /// </summary>
    public bool IsGuest { get; set; }

    /// <summary>
    /// The violator a complaint names, null for a ticket without one. Server-side only:
    /// what TicketVisibility compares with the reader; his name is a line of the text.
    /// </summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>
    /// Contact email left by a guest author (null for authenticated submissions).
    /// Kept out of the text: the address is for moderation to work with, an
    /// access recovery is checked against it.
    /// </summary>
    public string? GuestEmail { get; set; }

    /// <summary>
    /// Public tracking token for guest submissions (null for authenticated
    /// authors). Returned once on intake so the guest can check their ticket.
    /// </summary>
    public string? TrackingToken { get; set; }

    public Guid? EntityId { get; set; }

    public string? EntityType { get; set; }

    public TicketStatus Status { get; set; }

    public TicketSubtype Subtype { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// The text of the ticket, the lines of the form included
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// The reporter's comment of a complaint, the subject of a ticket filed from the form
    /// </summary>
    public string Comment { get; set; } = "";

    /// <summary>
    /// How many comments the conversation of the ticket holds
    /// </summary>
    public int CommentsCount { get; set; }

    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// The moderator who closed the ticket (null while it is open, and for an
    /// account removed since)
    /// </summary>
    public string? ResolvedByUsername { get; set; }
}
