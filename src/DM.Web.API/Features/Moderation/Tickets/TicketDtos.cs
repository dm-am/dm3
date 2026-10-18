using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;

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
    /// The reporter: null for a guest submission, and for an account removed since
    /// (<see cref="IsGuest" /> tells the two apart, the signature reads
    /// "гость" for the first and "Аккаунт удален" for the second)
    /// </summary>
    public User? Reporter { get; set; }

    /// <summary>
    /// Whether the ticket was filed without an account
    /// </summary>
    public bool IsGuest { get; set; }

    /// <summary>
    /// Contact email left by a guest author (null for authenticated submissions).
    /// For moderation to work with, an access recovery is checked against it; it
    /// is not a part of the text, which is the author's words alone
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
    /// Ticket body as it was stored, in the site markup: the text of a support
    /// or complaint form, or the reported content snapshot. The fields of the
    /// form that are not the text (the violator, the link, the contact of an
    /// author with an account) are lines of it, written there at filing
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Ticket body rendered for display, with the tag set of profile texts:
    /// neither [mod] nor [private] draws anything
    /// </summary>
    public InfoBbText DescriptionHtml { get; set; } = null!;

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
}

/// <summary>
/// A ticket with its conversation: the page of the ticket, for its author and for
/// moderation
/// </summary>
public class TicketDetails : Ticket
{
    /// <summary>
    /// The conversation, from the oldest comment to the newest. Whole: it holds a
    /// hundred comments at most
    /// </summary>
    public IEnumerable<TicketComment> Comments { get; set; } = [];

    /// <summary>
    /// Whether the caller may write in the conversation: the ticket is open and
    /// has room for another comment
    /// </summary>
    public bool CanComment { get; set; }

    /// <summary>
    /// Whether the caller answers as moderation and chooses with his answer the
    /// status the ticket moves to (AnswerTicketRequest.Status): it is open, and he
    /// moderates it without being its author
    /// </summary>
    public bool CanResolve { get; set; }

    /// <summary>
    /// The moderator who closed the ticket (null while it is open, and for an
    /// account removed since)
    /// </summary>
    public string? ResolvedByUsername { get; set; }
}

/// <summary>
/// One comment of the conversation of a ticket. Not edited, not deleted, not
/// liked: the conversation is the record of how the ticket was settled
/// </summary>
public class TicketComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The author: null for the guest who filed the ticket, and for an account
    /// removed since (<see cref="IsGuest" /> tells the two apart)
    /// </summary>
    public User? Author { get; set; }

    /// <summary>
    /// Whether the comment was written by the guest who filed the ticket
    /// </summary>
    public bool IsGuest { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Text rendered for display, with the tag set of comments, [mod] included
    /// </summary>
    public CommonBbText Text { get; set; } = null!;
}

/// <summary>
/// Request of the guest who filed a ticket to write in its conversation
/// </summary>
public class CreateTicketCommentRequest
{
    /// <summary>
    /// Text in the site markup, the tag set of comments; as long as the text of a
    /// ticket at most
    /// </summary>
    /// <example>Please send a link to the message</example>
    public string Text { get; set; } = "";
}

/// <summary>
/// Request to answer in the conversation of a ticket, as its author or as
/// moderation
/// </summary>
public class AnswerTicketRequest
{
    /// <summary>
    /// Text in the site markup, the tag set of comments; as long as the text of a
    /// ticket at most. Required, except for moderation closing the ticket or
    /// marking it as spam: an empty text then only changes the status
    /// </summary>
    /// <example>Please send a link to the message</example>
    public string Text { get; set; } = "";

    /// <summary>
    /// The status moderation moves the ticket to with the answer: WaitingForUser
    /// when left out, Closed or Spam. The author of a ticket sends none (403): his
    /// answer always leaves it waiting for moderation
    /// </summary>
    /// <example>WaitingForUser</example>
    public TicketStatus? Status { get; set; }
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
