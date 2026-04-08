using System;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Messaging.GeneralBus;

/// <summary>
/// Domain event message model for RabbitMQ transport
/// </summary>
public class InvokedEvent
{
    /// <summary>
    /// Event type (e.g., NewPost, NewComment, GameStatusChanged)
    /// </summary>
    public EventType Type { get; set; }

    /// <summary>
    /// Entity identifier (e.g., post ID, comment ID, or game ID)
    /// </summary>
    public Guid EntityId { get; set; }
}