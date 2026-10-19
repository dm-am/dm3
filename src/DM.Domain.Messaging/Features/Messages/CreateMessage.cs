using System;

namespace DM.Domain.Messaging.Features.Messages;

public class CreateMessage
{
    public Guid ChatId { get; set; }

    public string Text { get; set; } = null!;
}
