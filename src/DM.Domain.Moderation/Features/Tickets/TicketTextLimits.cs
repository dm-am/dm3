namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// How long a text of a ticket may be: the text the form files and every comment
/// of the conversation after it.
/// </summary>
/// <remarks>
/// One number for both, because both are written in the same place by the same
/// people, and shorter than a comment of a topic: the conversation comes back
/// whole with the ticket, a hundred comments at most, and at this length the
/// answer stays within a million characters of source text. The client keeps a
/// copy for the counter of its form (TICKET_TEXT_MAX_LENGTH), and the
/// architecture tests hold the copy to this one, as they hold the copy of the
/// comment limit.
/// </remarks>
public static class TicketTextLimits
{
    /// <summary>
    /// The longest text of a ticket or of a comment in it, in characters.
    /// </summary>
    public const int MaxLength = 10000;

    /// <summary>
    /// The most comments a conversation takes. Past it the next word goes into a
    /// new ticket: what has not been settled in a hundred comments is a new
    /// question.
    /// </summary>
    public const int MaxComments = 100;
}
