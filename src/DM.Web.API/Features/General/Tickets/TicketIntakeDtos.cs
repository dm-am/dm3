using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.General.Tickets;

/// <summary>
/// Request to create a ticket ("обращение") via the public intake forms
/// </summary>
public class CreateTicketIntakeRequest
{
    /// <summary>
    /// Ticket category
    /// </summary>
    /// <example>Bug</example>
    public TicketSubtype Subtype { get; set; }

    /// <summary>
    /// Short subject line
    /// </summary>
    /// <example>Cannot open the game page</example>
    public string Subject { get; set; } = "";

    /// <summary>
    /// Ticket text (BBCode)
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
    /// <remarks>
    /// This field is invisible to users but often filled by automated bots.
    /// If this field contains any value, the submission will be rejected.
    /// </remarks>
    public string? Website { get; set; }
}

/// <summary>
/// Response returned after a public intake submission
/// </summary>
public class CreateTicketIntakeResponse
{
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
/// Deliberately narrow — it omits moderation internals (assignee, warnings,
/// bans, target user) and exposes only what the guest author needs.
/// </summary>
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
    /// Ticket body text
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Creation time
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Resolution time (null until resolved)
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Moderation's answer (null until answered)
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    /// Conversation thread, oldest first
    /// </summary>
    public IEnumerable<TrackedTicketResponse> Responses { get; set; } = [];
}

/// <summary>
/// A single response in the guest-facing ticket thread
/// </summary>
public class TrackedTicketResponse
{
    /// <summary>
    /// Response text
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// When the response was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Whether this response is from a moderator (vs the ticket author)
    /// </summary>
    public bool IsFromModerator { get; set; }
}
