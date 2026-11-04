using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

public class CreateGlobalChatEventEntity
{
    public Guid GlobalChatEventId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset StartsUtc { get; set; }

    public TimeSpan Duration { get; set; }

    public bool IsOpen { get; set; }

    public GlobalChatEventStatus Status { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}
