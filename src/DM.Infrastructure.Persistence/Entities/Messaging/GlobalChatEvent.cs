using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Messaging;

/// <summary>
/// DAL model for events in global chat.
/// Events are only supported for the global chat (Chat.GlobalChatId).
/// Only one event can be Live at a time.
/// </summary>
[Table("GlobalChatEvents")]
public class GlobalChatEvent
{
    /// <summary>
    /// Event identifier
    /// </summary>
    [Key]
    public Guid GlobalChatEventId { get; set; }

    /// <summary>
    /// Event title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Event description (BBCode)
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Scheduled start time (for display purposes, actual start is manual)
    /// </summary>
    public DateTimeOffset StartsUtc { get; set; }

    /// <summary>
    /// Planned duration. Null means no time limit, event ends manually.
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
    /// User who created the event
    /// </summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// Event creation timestamp
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
    /// Event creator
    /// </summary>
    [ForeignKey(nameof(CreatedByUserId))]
    public virtual User CreatedBy { get; set; } = null!;

    /// <summary>
    /// Event participants (for closed events, determines who can send messages)
    /// </summary>
    [InverseProperty(nameof(GlobalChatEventParticipant.GlobalChatEvent))]
    public virtual ICollection<GlobalChatEventParticipant> Participants { get; set; } = [];

    /// <summary>
    /// Messages sent during this event
    /// </summary>
    [InverseProperty(nameof(Message.GlobalChatEvent))]
    public virtual ICollection<Message> Messages { get; set; } = [];
}
