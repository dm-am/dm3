using System;

namespace DM.Domain.Messaging.Features.Messages;

public class UpdateMessage
{
    public Guid MessageId { get; set; }

    /// <summary>
    /// New text, null keeps the stored one
    /// </summary>
    public string? Text { get; set; }
}
