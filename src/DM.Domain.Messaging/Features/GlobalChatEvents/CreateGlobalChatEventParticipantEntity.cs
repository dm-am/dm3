using System;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

public class CreateGlobalChatEventParticipantEntity
{
    public Guid GlobalChatEventParticipantId { get; set; }

    public Guid GlobalChatEventId { get; set; }

    public Guid UserId { get; set; }

    public bool IsOrganizer { get; set; }

    public DateTimeOffset JoinedUtc { get; set; }
}
