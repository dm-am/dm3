using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Tickets;

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
