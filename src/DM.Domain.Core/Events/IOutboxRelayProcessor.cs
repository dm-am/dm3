using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Events;

/// <summary>
/// One stored domain event, as the relay hands it to its publish delegate.
/// </summary>
/// <param name="EventId">Identity of the publication (W1.4): every republication
/// of one row carries the same value, which is what lets the consumer treat a
/// relay retry as a replay rather than a second event.</param>
/// <param name="EventType">Type of the event.</param>
/// <param name="EntityId">Entity the event reports on.</param>
public sealed record OutboxEnvelope(Guid EventId, EventType EventType, Guid EntityId);

/// <summary>
/// What one relay pass did and what it left behind.
/// </summary>
/// <param name="Published">Rows published with confirmation and marked.</param>
/// <param name="Faulted">Whether the batch stopped on a refused row. Tells the
/// host not to keep draining until the next tick.</param>
/// <param name="Pending">Unpublished rows left after the pass.</param>
/// <param name="OldestPendingAge">Age of the oldest unpublished row; zero when
/// the backlog is empty. The number behind the backlog alert.</param>
public sealed record OutboxRelayResult(
    int Published, bool Faulted, int Pending, TimeSpan OldestPendingAge);

/// <summary>
/// One ordered batch of the outbox relay: the storage half of at-least-once
/// event delivery.
/// </summary>
/// <remarks>
/// Which table the events wait in and how a batch is claimed belongs to the
/// persistence layer, the same split as <see cref="Retention.IRetentionSweepProcessor"/>;
/// this contract only lets the host run one batch through its own publisher.
/// </remarks>
public interface IOutboxRelayProcessor
{
    /// <summary>
    /// How many rows one batch claims. A code constant rather than configuration:
    /// large enough to drain any realistic backlog in seconds at one batch per
    /// tick, small enough to keep the claim transaction short while every row
    /// waits on a publisher confirm.
    /// </summary>
    public const int BatchSize = 100;

    /// <summary>Claims one ordered batch, publishes each row through the
    /// delegate, marks the confirmed prefix, and reports what is left.</summary>
    /// <param name="publish">Publication with confirmation; an exception means
    /// "the row did not leave" and stops the batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<OutboxRelayResult> RelayBatchAsync(
        Func<OutboxEnvelope, CancellationToken, Task> publish,
        CancellationToken cancellationToken);
}
