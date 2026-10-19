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

    /// <summary>
    /// Identity of this publication, distinct from the entity it reports on.
    /// </summary>
    /// <remarks>
    /// Stamped by OutboxEventProducer; a relay retry and a redelivery carry the same id.
    /// <see cref="Guid.Empty"/> is a message without the field, processed without deduplication.
    /// </remarks>
    public Guid EventId { get; set; }
}
