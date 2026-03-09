using System;

namespace DM.Domain.Messaging.Features.Messages;

/// <summary>
/// DTO for creating a message entity in repository
/// </summary>
public class CreateMessageEntity
{
    /// <summary>
    /// Message identifier
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Message text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Whether the message is removed
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Global chat event ID (if message is part of an event)
    /// </summary>
    public Guid? GlobalChatEventId { get; set; }
}

/// <summary>
/// DTO for updating chat's last message
/// </summary>
public class UpdateChatLastMessageEntity
{
    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// Last message identifier
    /// </summary>
    public Guid LastMessageId { get; set; }
}
