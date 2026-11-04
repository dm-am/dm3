using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Shared.BotTrap;
using DM.Web.API.Features.Moderation.Tickets;
using DM.Web.API.Shared.BbRendering;

namespace DM.Web.API.Features.General.Tickets;

/// <summary>
/// Request to create a ticket ("обращение") via the public intake forms
/// </summary>
public class CreateTicketIntakeRequest : IHoneypotForm
{
    /// <summary>
    /// Ticket category
    /// </summary>
    /// <example>Bug</example>
    public TicketSubtype Subtype { get; set; }

    /// <summary>
    /// Optional short subject line. The support and complaint forms do not ask
    /// for one: the screens that read a ticket back name it by its category.
    /// </summary>
    /// <example>Cannot open the game page</example>
    public string? Subject { get; set; }

    /// <summary>
    /// Ticket text in the site markup, the tag set of profile texts: neither
    /// [mod] nor [private]
    /// </summary>
    /// <example>The page shows an error when I open it...</example>
    public string Text { get; set; } = "";

    /// <summary>
    /// Optional contact for the reply (stored as guest email for unauthenticated authors)
    /// </summary>
    /// <example>user@example.com</example>
    public string? Contact { get; set; }

    /// <summary>
    /// Optional link to the reported violation (complaints)
    /// </summary>
    public string? ViolationUrl { get; set; }

    /// <summary>
    /// Optional username of the user the complaint is about
    /// </summary>
    public string? TargetUsername { get; set; }

    /// <summary>
    /// Honeypot field for bot protection - must be left empty
    /// </summary>
    public string? Website { get; set; }

    /// <inheritdoc />
    (string Field, string Sentence) IHoneypotForm.Refusal => ("subject", RefusalMessage.TicketNotFiled);
}

/// <summary>
/// Response returned after a public intake submission
/// </summary>
public class CreateTicketIntakeResponse
{
    /// <summary>
    /// The ticket just filed. The form goes on to the page of the ticket: an
    /// authenticated author's is addressed by this identifier, a guest's by the
    /// tracking token below.
    /// </summary>
    public Guid TicketId { get; set; }

    /// <summary>
    /// Public tracking token for guest submissions. Present only for guests —
    /// null for authenticated authors, who track their tickets from
    /// "Мои обращения". Sent back on the guest tracking call in the
    /// X-Dm-Ticket-Token header (GET /v1/tickets/track).
    /// </summary>
    public string? TrackingToken { get; set; }
}

/// <summary>
/// Public, token-gated view of a ticket for the guest tracking page.
/// </summary>
/// <remarks>
/// Narrow on purpose: nothing of the moderation's internals, but the conversation
/// with its authors and the moderator who closed the ticket, whose names are open
/// on the page of the rules anyway.
/// </remarks>
public class TrackedTicket
{
    /// <summary>
    /// Ticket status
    /// </summary>
    public TicketStatus Status { get; set; }

    /// <summary>
    /// Ticket category
    /// </summary>
    public TicketSubtype Subtype { get; set; }

    /// <summary>
    /// Short subject line
    /// </summary>
    public string Subject { get; set; } = "";

    /// <summary>
    /// Ticket body rendered for display, with the tag set of profile texts
    /// </summary>
    public InfoBbText Description { get; set; } = null!;

    /// <summary>
    /// Creation time
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Resolution time (null until resolved)
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// The moderator who closed the ticket (null while it is open)
    /// </summary>
    public string? ResolvedByUsername { get; set; }

    /// <summary>
    /// The conversation, from the oldest comment to the newest
    /// </summary>
    public IEnumerable<TicketComment> Comments { get; set; } = [];

    /// <summary>
    /// Whether the guest may write in the conversation: the ticket is open and has
    /// room for another comment
    /// </summary>
    public bool CanComment { get; set; }
}
