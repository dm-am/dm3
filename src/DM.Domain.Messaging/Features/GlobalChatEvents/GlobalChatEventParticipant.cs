using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// Service DTO for chat event participant
/// </summary>
public class GlobalChatEventParticipant
{
    /// <summary>
    /// Participant link identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The user
    /// </summary>
    public GeneralUser User { get; set; } = null!;

    /// <summary>
    /// Whether this participant is an organizer
    /// </summary>
    public bool IsOrganizer { get; set; }

    /// <summary>
    /// When the participant joined the event
    /// </summary>
    public DateTimeOffset JoinedAt { get; set; }
}
