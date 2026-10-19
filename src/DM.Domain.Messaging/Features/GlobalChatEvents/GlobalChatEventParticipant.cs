using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

public class GlobalChatEventParticipant
{
    /// <summary>
    /// Participant link identifier
    /// </summary>
    public Guid Id { get; set; }

    public GeneralUser User { get; set; } = null!;

    public bool IsOrganizer { get; set; }

    public DateTimeOffset JoinedUtc { get; set; }
}
