using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

public class GlobalChatEvent
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>
    /// Event description in BBCode
    /// </summary>
    public string Description { get; set; } = null!;

    public DateTimeOffset StartsUtc { get; set; }

    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Whether every signed-in user may write during the event, or only the participants
    /// the organizer added
    /// </summary>
    public bool IsOpen { get; set; }

    public GlobalChatEventStatus Status { get; set; }

    public GeneralUser CreatedBy { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the event actually started, null before it did
    /// </summary>
    public DateTimeOffset? StartedUtc { get; set; }

    /// <summary>
    /// When the event ended, null before it did
    /// </summary>
    public DateTimeOffset? EndedUtc { get; set; }

    public IEnumerable<GlobalChatEventParticipant> Participants { get; set; } = [];
}
