using System;

namespace DM.Domain.Messaging.Features.Messages;

/// <summary>
/// DTO for updating message
/// </summary>
public class UpdateMessage
{
    /// <summary>
    /// Message identifier
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Message content
    /// </summary>
    public string? Text { get; set; }
}
