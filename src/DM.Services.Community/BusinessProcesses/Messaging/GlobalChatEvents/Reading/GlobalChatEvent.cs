using System;
using System.Collections.Generic;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

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
    /// Scheduled start time
    /// </summary>
    public DateTimeOffset StartsAt { get; set; }

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
    /// Event creation timestamp
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the event was actually started (null if not started yet)
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When the event was ended (null if not ended yet)
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// Event participants
    /// </summary>
    public IEnumerable<GlobalChatEventParticipant> Participants { get; set; } = [];
}
