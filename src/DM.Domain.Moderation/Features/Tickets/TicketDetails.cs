using System.Collections.Generic;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// A ticket with its conversation, as its page reads it
/// </summary>
public class TicketDetails : Ticket
{
    /// <summary>
    /// The conversation, from the oldest comment to the newest
    /// </summary>
    public IReadOnlyList<TicketComment> Comments { get; set; } = [];

    /// <summary>
    /// Whether the caller may write in the conversation: the ticket is open, it has
    /// room for another comment, and the caller is its author or moderates it.
    /// Set by the service for the caller.
    /// </summary>
    public bool CanComment { get; set; }

    /// <summary>
    /// Whether the caller answers as moderation and chooses with his answer the
    /// status the ticket moves to, closing it among them: it is open and the
    /// caller moderates it without being its author. Set by the service for the
    /// caller.
    /// </summary>
    public bool CanResolve { get; set; }
}
