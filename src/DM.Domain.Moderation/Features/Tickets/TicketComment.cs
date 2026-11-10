using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// One comment of the conversation of a ticket
/// </summary>
public class TicketComment
{
    public Guid TicketCommentId { get; set; }

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

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Text in the site markup, the tag set of comments
    /// </summary>
    public string Text { get; set; } = "";
}
