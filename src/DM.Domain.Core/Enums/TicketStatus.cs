namespace DM.Domain.Core.Enums;

/// <summary>
/// Status of a moderation ticket ("обращение")
/// </summary>
public enum TicketStatus
{
    /// <summary>
    /// Ticket is waiting for a moderation response
    /// </summary>
    WaitingForModeration = 0,

    /// <summary>
    /// Moderation has answered and is waiting for the user
    /// </summary>
    WaitingForUser = 1,

    /// <summary>
    /// Ticket conversation is finished
    /// </summary>
    Closed = 2,

    /// <summary>
    /// Ticket was marked as spam
    /// </summary>
    Spam = 3
}
