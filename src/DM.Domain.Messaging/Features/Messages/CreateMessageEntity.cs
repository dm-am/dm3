using System;

namespace DM.Domain.Messaging.Features.Messages;

public class CreateMessageEntity
{
    public Guid MessageId { get; set; }

    public Guid ChatId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid UserId { get; set; }

    public string Text { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }

    public bool IsRemoved { get; set; }

    /// <summary>
    /// Global chat event the message is written during, if any
    /// </summary>
    public Guid? GlobalChatEventId { get; set; }
}
