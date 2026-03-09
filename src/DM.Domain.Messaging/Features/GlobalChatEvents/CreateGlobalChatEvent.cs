using System;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// DTO for creating a chat event
/// </summary>
public class CreateGlobalChatEvent
{
    /// <summary>
    /// Event title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Event description (BBCode)
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Scheduled start time (UTC)
    /// </summary>
    public DateTimeOffset StartsAt { get; set; }

    /// <summary>
    /// Planned duration. Null means no time limit.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// If true, all authenticated users can participate.
    /// If false, only invited participants can send messages.
    /// </summary>
    public bool IsOpen { get; set; }
}
