using System;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

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
