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
    /// Target user identifier (null for tickets without a target).
    /// Server-side only — what the visibility rule compares with the reader: the
    /// violator a complaint names does not moderate it (TicketVisibility). The
    /// name of the violator is a line of the text, written there at filing.
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
    /// How many comments the conversation of the ticket holds
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Resolution time
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// The moderator who closed the ticket (null while it is open, and for an
    /// account removed since)
    /// </summary>
    public string? ResolvedByUsername { get; set; }
}

/// <summary>
/// A ticket with its conversation, as its page reads it
/// </summary>
public class TicketDetails : Ticket
{
    /// <summary>
    /// The conversation, from the oldest comment to the newest
    /// </summary>
    public IReadOnlyList<TicketComment> Comments { get; set; } = [];

    /// <summary>
    /// Whether the caller may write in the conversation: the ticket is open, it has
    /// room for another comment, and the caller is its author or moderates it.
    /// Set by the service for the caller.
    /// </summary>
    public bool CanComment { get; set; }

    /// <summary>
    /// Whether the caller answers as moderation and chooses with his answer the
    /// status the ticket moves to, closing it among them: it is open and the
    /// caller moderates it without being its author. Set by the service for the
    /// caller.
    /// </summary>
    public bool CanResolve { get; set; }
}

/// <summary>
/// One comment of the conversation of a ticket
/// </summary>
public class TicketComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid TicketCommentId { get; set; }

    /// <summary>
    /// The ticket the comment is written in
    /// </summary>
    public Guid TicketId { get; set; }

    /// <summary>
    /// The author, null for the guest who filed the ticket and for a removed
    /// account alike: <see cref="IsGuest" /> tells the two apart
    /// </summary>
    public GeneralUser? Author { get; set; }

    /// <summary>
    /// Whether the comment was written by the guest who filed the ticket, on the
    /// tracking page
    /// </summary>
    public bool IsGuest { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Text in the site markup, the tag set of comments
    /// </summary>
    public string Text { get; set; } = "";
}

/// <summary>
/// DTO for an answer in the conversation of a ticket: the text, and for
/// moderation the status the ticket moves to with it
/// </summary>
public class CreateTicketComment
{
    /// <summary>
    /// Text in the site markup, the tag set of comments. May stay empty only when
    /// moderation closes the ticket or marks it as spam: then the status is all
    /// the answer changes
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// The status moderation moves the ticket to: waiting for the user when left
    /// out, closed, or spam. The author of a ticket does not choose one: his
    /// answer always leaves it waiting for moderation
    /// </summary>
    public TicketStatus? Status { get; set; }
}

/// <summary>
/// What became of a comment the repository was asked to add
/// </summary>
public enum TicketCommentOutcome
{
    /// <summary>
    /// The comment is in the conversation, and the status of the ticket is the one
    /// asked for
    /// </summary>
    Added,

    /// <summary>
    /// The ticket was closed by the time the comment arrived: nothing is written
    /// </summary>
    Closed,

    /// <summary>
    /// The conversation holds as many comments as it takes: nothing is written
    /// </summary>
    Full
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
/// Entity DTO for adding a comment to the conversation of a ticket (repository
/// level)
/// </summary>
public class CreateTicketCommentEntity
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid TicketCommentId { get; set; }

    /// <summary>
    /// The ticket the comment is written in
    /// </summary>
    public Guid TicketId { get; set; }

    /// <summary>
    /// The author, null for the guest who filed the ticket: the only comment
    /// written without an account is one written by the tracking token
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Text in the site markup, as stored
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
