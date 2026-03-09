using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// DTO for updating a global chat event entity in repository
/// </summary>
public class UpdateGlobalChatEventEntity
{
    /// <summary>
    /// Event identifier
    /// </summary>
    public Guid GlobalChatEventId { get; set; }

    /// <summary>
    /// Updated title (null to keep current)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Updated description (null to keep current)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Updated start time (null to keep current)
    /// </summary>
    public DateTimeOffset? StartsAtUtc { get; set; }

    /// <summary>
    /// Updated duration (null to keep current)
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Updated open status (null to keep current)
    /// </summary>
    public bool? IsOpen { get; set; }

    /// <summary>
    /// Updated status (null to keep current)
    /// </summary>
    public GlobalChatEventStatus? Status { get; set; }

    /// <summary>
    /// Updated started timestamp (null to keep current)
    /// </summary>
    public DateTimeOffset? StartedAtUtc { get; set; }

    /// <summary>
    /// Updated ended timestamp (null to keep current)
    /// </summary>
    public DateTimeOffset? EndedAtUtc { get; set; }
}
