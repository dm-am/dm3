using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// Service DTO for chat event
/// </summary>
public class GlobalChatEvent
{
    /// <summary>
    /// Event identifier
    /// </summary>
    public Guid Id { get; set; }

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
    public DateTimeOffset StartsUtc { get; set; }

    /// <summary>
    /// Planned duration. Null means no time limit.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// If true, all authenticated users can send messages during the event.
    /// If false, only participants added by organizer can send messages.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Current event status
    /// </summary>
    public GlobalChatEventStatus Status { get; set; }

    /// <summary>
    /// Event creator
    /// </summary>
    public GeneralUser CreatedBy { get; set; } = null!;

    /// <summary>
    /// Event creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the event was actually started (null if not started yet)
    /// </summary>
    public DateTimeOffset? StartedUtc { get; set; }

    /// <summary>
    /// When the event was ended (null if not ended yet)
    /// </summary>
    public DateTimeOffset? EndedUtc { get; set; }

    /// <summary>
    /// Event participants
    /// </summary>
    public IEnumerable<GlobalChatEventParticipant> Participants { get; set; } = [];
}
