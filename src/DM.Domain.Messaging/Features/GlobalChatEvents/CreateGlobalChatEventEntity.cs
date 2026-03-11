using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// DTO for creating a global chat event entity in repository
/// </summary>
public class CreateGlobalChatEventEntity
{
    /// <summary>
    /// Event identifier
    /// </summary>
    public Guid GlobalChatEventId { get; set; }

    /// <summary>
    /// Event title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Event description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Event start time
    /// </summary>
    public DateTimeOffset StartsAtUtc { get; set; }

    /// <summary>
    /// Event duration
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether the event is open for all users
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Event status
    /// </summary>
    public GlobalChatEventStatus Status { get; set; }

    /// <summary>
    /// Creator user ID
    /// </summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// DTO for creating a global chat event participant entity
/// </summary>
public class CreateGlobalChatEventParticipantEntity
{
    /// <summary>
    /// Participant identifier
    /// </summary>
    public Guid GlobalChatEventParticipantId { get; set; }

    /// <summary>
    /// Event identifier
    /// </summary>
    public Guid GlobalChatEventId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Whether the participant is an organizer
    /// </summary>
    public bool IsOrganizer { get; set; }

    /// <summary>
    /// Join timestamp
    /// </summary>
    public DateTimeOffset JoinedAtUtc { get; set; }
}
