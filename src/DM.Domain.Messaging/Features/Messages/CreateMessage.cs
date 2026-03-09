using System;

namespace DM.Domain.Messaging.Features.Messages;

/// <summary>
/// DTO for creating message
/// </summary>
public class CreateMessage
{
    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// Message content
    /// </summary>
    public string Text { get; set; } = null!;
}