using System;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Outbox;

/// <summary>
/// DAL model for one outbox row: an event or a letter that waits for the worker
/// of its <see cref="Queue"/>.
/// </summary>
/// <remarks>
/// Events and letters share the table: the event columns are empty on
/// a letter and <see cref="Payload"/> is empty on an event. The database does not
/// enforce which shape belongs to which queue, the places that insert rows do.
/// An event carries no payload: its message is entirely reconstructible from
/// (EventType, EntityId, EventId). No foreign key on <see cref="EntityId"/>
/// either - it points into different tables of different modules, and the event
/// is a fact of its own that has to outlive the entity it reports on.
/// </remarks>
[Table("OutboxEvents")]
public class OutboxEvent
{
    /// <summary>
    /// Insertion order, identity. The tie-break of the claim order when two rows
    /// carry the same <see cref="OccurredUtc"/>.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Identity of the row, stamped at insertion and never changed across
    /// retries, so the consumer's (EventId, EventType) unique index swallows a
    /// second handling of one event.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Queue the row belongs to, one of QueueNames: what tells an event from a
    /// letter in this shared table.
    /// </summary>
    public string Queue { get; set; } = null!;

    /// <summary>
    /// Event type, stored as its number: a name string would jam the queue forever
    /// after a deployment rollback removed the name. NULL on a letter.
    /// </summary>
    public EventType? EventType { get; set; }

    /// <summary>
    /// Entity the event reports on. NULL on a letter.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// The letter, serialized. Plain text and not a JSON column, because the JSON
    /// type refuses a zero character and letters carry text typed by users. NULL
    /// on an event.
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// W3C traceparent of the request that stored the row, so the worker's span
    /// continues its trace. NULL when the trace identifier was in another format.
    /// </summary>
    public string? TraceParent { get; set; }

    /// <summary>
    /// Insertion moment from IDateTimeProvider; the first key of the claim order.
    /// </summary>
    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>
    /// The moment before which the row must not be claimed. NULL when it may be
    /// claimed now.
    /// </summary>
    public DateTimeOffset? NextAttemptUtc { get; set; }

    /// <summary>
    /// NULL until the row is processed. What the retention sweep reads: a row
    /// that is not processed never matches the cutoff predicate and is never
    /// deleted automatically.
    /// </summary>
    public DateTimeOffset? ProcessedUtc { get; set; }

    /// <summary>
    /// The moment the row became dead. NULL while it is alive.
    /// </summary>
    public DateTimeOffset? DeadUtc { get; set; }

    /// <summary>
    /// Failed handlings of this row.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Type and message of the last failure, truncated to 500 characters.
    /// </summary>
    public string? LastError { get; set; }
}
