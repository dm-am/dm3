namespace DM.Domain.Moderation.Features.Tickets;

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
