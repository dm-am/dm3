using System;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// Input DTO for updating a chat event
/// </summary>
public class UpdateGlobalChatEvent
{
    /// <summary>
    /// Event identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Event title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Event description (BBCode)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Planned start time (UTC)
    /// </summary>
    public DateTimeOffset? StartsUtc { get; set; }

    /// <summary>
    /// Event duration (null = no limit)
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// If true, all authenticated users can send messages during the event
    /// </summary>
    public bool? IsOpen { get; set; }
}
