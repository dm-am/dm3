using System;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Outbox;

/// <summary>
/// DAL model for one stored domain event: the transactional outbox between the
/// domain services and the broker.
/// </summary>
/// <remarks>
/// A row is written by OutboxEventProducer in its own commit right after the
/// domain one, published by the relay with a publisher confirm, and marked with
/// <see cref="PublishedUtc"/> only once the broker confirmed. No payload column:
/// the wire message is entirely reconstructible from (EventType, EntityId,
/// EventId), so a body here would be a copy of three columns and a second
/// serialization contract. No foreign key on <see cref="EntityId"/> either -
/// it points into different tables of different modules, and the event is a
/// fact of its own that has to outlive the entity it reports on.
/// </remarks>
[Table("OutboxEvents")]
public class OutboxEvent
{
    /// <summary>
    /// Insertion order, identity. The tie-break of the publication order when
    /// two rows carry the same <see cref="OccurredUtc"/>.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Identity of the publication (W1.4), stamped at insertion. Every
    /// republication of this row carries the same value, so the consumer's
    /// (EventId, EventType) unique index swallows relay duplicates.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Event type, stored as its number the way Notifications stores it: a name
    /// string would jam the relay forever after a deployment rollback removed
    /// the name. A number the RELAY's assembly does not know still jams the
    /// head of the backlog - ToRoutingKeys throws before any publish - but
    /// with Attempts/LastError diagnostics and a manual way out (design, R5).
    /// Only a number unknown to the WORKER becomes an unroutable publish the
    /// broker drops, exactly what the old direct producer did.
    /// </summary>
    public EventType EventType { get; set; }

    /// <summary>
    /// Entity the event reports on.
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Insertion moment from IDateTimeProvider; the first key of the
    /// publication order.
    /// </summary>
    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>
    /// NULL until the broker confirms the publication of exactly this row.
    /// What the retention sweep reads: an unpublished row never matches the
    /// cutoff predicate and is never deleted automatically.
    /// </summary>
    public DateTimeOffset? PublishedUtc { get; set; }

    /// <summary>
    /// Failed relay passes over this row. Diagnostics for a human reading the
    /// backlog alert, never a scheduler: there is no attempt ceiling and no
    /// dead-letter table, because moving a row aside changes no fact and only
    /// hides the backlog from the alert.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Type and message of the last publish refusal, truncated to 500
    /// characters.
    /// </summary>
    public string? LastError { get; set; }
}
