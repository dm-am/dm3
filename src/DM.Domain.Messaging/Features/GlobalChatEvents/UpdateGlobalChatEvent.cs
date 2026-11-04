using System;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// An edit of a global chat event: a field left null keeps what is stored
/// </summary>
public class UpdateGlobalChatEvent
{
    public Guid Id { get; set; }

    public string? Title { get; set; }

    /// <summary>
    /// Event description in BBCode
    /// </summary>
    public string? Description { get; set; }

    public DateTimeOffset? StartsUtc { get; set; }

    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Whether every signed-in user may write during the event
    /// </summary>
    public bool? IsOpen { get; set; }
}
