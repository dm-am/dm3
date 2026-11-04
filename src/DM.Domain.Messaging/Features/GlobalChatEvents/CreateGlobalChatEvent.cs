using System;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

public class CreateGlobalChatEvent
{
    public string Title { get; set; } = null!;

    /// <summary>
    /// Event description in BBCode, optional
    /// </summary>
    public string? Description { get; set; }

    public DateTimeOffset StartsUtc { get; set; }

    /// <summary>
    /// Planned duration, four hours when left out
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Whether every signed-in user may write during the event, or only its participants
    /// </summary>
    public bool IsOpen { get; set; }
}
