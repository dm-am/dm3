using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Messaging.GlobalChatEvents;

/// <summary>
/// API DTO for chat event
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
    /// Event description (BBCode rendered)
    /// </summary>
    public CommonBbText Description { get; set; } = null!;

    /// <summary>
    /// Planned start time (UTC)
    /// </summary>
    public DateTimeOffset StartsUtc { get; set; }

    /// <summary>
    /// Event duration (null = no limit)
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// If true, all authenticated users can send messages during the event.
    /// If false, only participants added by organizer can send messages.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Event status (Scheduled, Live, Ended)
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GlobalChatEventStatus Status { get; set; }

    /// <summary>
    /// Event creator
    /// </summary>
    public User CreatedBy { get; set; } = null!;

    /// <summary>
    /// When the event was created (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the event actually started (UTC). Absent until it does: the start is
    /// manual and does not have to fall on StartsUtc, so the planned start plus
    /// the duration is not an answer to how long the event has been running.
    /// </summary>
    public DateTimeOffset? StartedUtc { get; set; }

    /// <summary>
    /// When the event actually ended (UTC). Absent while it is still running.
    /// </summary>
    public DateTimeOffset? EndedUtc { get; set; }

    /// <summary>
    /// Event participants
    /// </summary>
    public IEnumerable<GlobalChatEventParticipant> Participants { get; set; } = [];
}

/// <summary>
/// API DTO for chat event participant
/// </summary>
public class GlobalChatEventParticipant
{
    /// <summary>
    /// Participant identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User profile
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Whether this participant is an organizer (can manage event)
    /// </summary>
    public bool IsOrganizer { get; set; }

    /// <summary>
    /// When the user joined the event (UTC)
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }
}

/// <summary>
/// Summary DTO for chat event (used in lists and global chat info)
/// </summary>
public class GlobalChatEventSummary
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
    /// Planned start time (UTC)
    /// </summary>
    public DateTimeOffset StartsUtc { get; set; }

    /// <summary>
    /// Event status
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GlobalChatEventStatus Status { get; set; }

    /// <summary>
    /// If true, all authenticated users can participate
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Number of participants
    /// </summary>
    public int ParticipantCount { get; set; }
}

/// <summary>
/// Input DTO for creating a chat event
/// </summary>
public class CreateGlobalChatEventInput
{
    /// <summary>
    /// Event title
    /// </summary>
    [Required(ErrorMessage = "Введите заголовок")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Заголовок от 1 до 200 символов")]
    public string Title { get; set; } = null!;

    /// <summary>
    /// Event description (BBCode)
    /// </summary>
    [StringLength(10000, ErrorMessage = "Описание не длиннее 10000 символов")]
    public string? Description { get; set; }

    /// <summary>
    /// Planned start time (UTC)
    /// </summary>
    public DateTimeOffset StartsUtc { get; set; }

    /// <summary>
    /// Event duration (null = no limit)
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// If true, all authenticated users can send messages during the event
    /// </summary>
    public bool IsOpen { get; set; }
}

/// <summary>
/// Input DTO for updating a chat event
/// </summary>
public class UpdateGlobalChatEventInput
{
    /// <summary>
    /// Event title
    /// </summary>
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Заголовок от 1 до 200 символов")]
    public string? Title { get; set; }

    /// <summary>
    /// Event description (BBCode)
    /// </summary>
    [StringLength(10000, ErrorMessage = "Описание не длиннее 10000 символов")]
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

/// <summary>
/// Input DTO for adding a participant to a closed event
/// </summary>
public class AddParticipantInput
{
    /// <summary>
    /// User username to add
    /// </summary>
    [Required(ErrorMessage = "Введите имя пользователя")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Имя пользователя от 1 до 20 символов")]
    public string Username { get; set; } = null!;

    /// <summary>
    /// Make this user an organizer
    /// </summary>
    public bool IsOrganizer { get; set; }
}
