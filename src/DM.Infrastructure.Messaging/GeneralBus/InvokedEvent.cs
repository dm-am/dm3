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
    /// Stamped by OutboxEventProducer when the event's outbox row is written -
    /// the single point every publication funnels through - and carried
    /// unchanged by every relay publish of that row. Two publications about one
    /// entity therefore carry two ids, while a relay retry and a broker
    /// redelivery of one publication carry the same id — which is exactly the
    /// difference a consumer needs to write idempotently.
    ///
    /// The codec is case-insensitive on read, so a message queued before this
    /// field existed decodes with <see cref="Guid.Empty"/> here. Empty means
    /// "pre-EventId era": consumers process such a message without
    /// deduplication, as they always had, rather than treating every legacy
    /// message as a replay of one and the same event.
    /// </remarks>
    public Guid EventId { get; set; }
}
