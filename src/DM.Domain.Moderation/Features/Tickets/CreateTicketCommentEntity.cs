using System;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Entity DTO for adding a comment to the conversation of a ticket (repository
/// level)
/// </summary>
public class CreateTicketCommentEntity
{
    public Guid TicketCommentId { get; set; }

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

    public DateTimeOffset CreatedUtc { get; set; }
}
