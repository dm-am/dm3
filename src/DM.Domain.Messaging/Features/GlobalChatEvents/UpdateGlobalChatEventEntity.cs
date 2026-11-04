using System;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// DTO for updating a global chat event entity in repository: a field left null keeps
/// the current value
/// </summary>
public class UpdateGlobalChatEventEntity
{
    public Guid GlobalChatEventId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset? StartsUtc { get; set; }

    public TimeSpan? Duration { get; set; }

    public bool? IsOpen { get; set; }
}
