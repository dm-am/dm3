namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// How long a text of a ticket may be: the text the form files and every comment
/// of the conversation after it.
/// </summary>
/// <remarks>
/// Shorter than a comment of a topic: the conversation comes back whole with the ticket,
/// and at this length its answer stays within a million characters. The client keeps a
/// copy for its counter (TICKET_TEXT_MAX_LENGTH), held to this one by the architecture tests.
/// </remarks>
public static class TicketTextLimits
{
    public const int MaxLength = 10000;

    /// <summary>
    /// The most comments a conversation takes; the next word goes into a new ticket.
    /// </summary>
    public const int MaxComments = 100;

    /// <summary>
    /// The fields of the intake form beside its text.
    /// </summary>
    public const int SubjectMaxLength = 200;

    public const int ContactMaxLength = 200;

    public const int ViolationUrlMaxLength = 500;
}
