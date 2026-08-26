using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Infrastructure.Core.Tracing;
using DM.Infrastructure.Persistence.Entities.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Event producer implementation that stores domain events in the outbox table
/// for the relay to publish.
/// </summary>
/// <remarks>
/// The replacement for the producer that published straight to RabbitMQ: from
/// here on, "publishing" an event is one INSERT into the same PostgreSQL the
/// write itself went to, and a broker restart turns from a silent loss into a
/// delivery delay. The insert is a second, separate commit of the scoped
/// context - deliberately outside the domain transaction, which lives inside a
/// repository method this producer is called after (the W1.1 compromise). This
/// class never opens a transaction and never joins one; EventOutboxShould holds
/// that as text.
///
/// The SaveChanges here flushes everything the scoped change tracker holds, so
/// a SendAsync called before the domain commit would commit somebody else's
/// half-written changes. PublishAfterCommitShould is what keeps the order.
/// </remarks>
internal class OutboxEventProducer(
    DmDbContext dbContext,
    IGuidFactory guidFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<OutboxEventProducer> logger) : IEventProducer
{
    /// <inheritdoc />
    public Task SendAsync(EventType eventType, Guid entityId) => SendAsync([eventType], entityId);

    /// <inheritdoc />
    /// <remarks>
    /// A refusal is logged and counted, never thrown - the same contract the
    /// direct producer kept, word for word. The write that produced the event is
    /// committed by the time this runs, so a throw would turn a saved comment
    /// into a 500 and the caller would post it twice. The refusal window is now
    /// PostgreSQL refusing right after accepting the domain commit - a pool run
    /// dry, a full disk - orders of magnitude narrower than the broker being
    /// down, and the one loss nothing will retry: hence the PublishFailed
    /// counter behind the EventsNotPublished alert.
    ///
    /// All rows of a batch go in one SaveChanges - atomically, unlike the N
    /// independent publications this replaced. Each row still gets its own
    /// EventId: the consumer deduplicates replays by it, so it has to be the
    /// same on every relay retry of one row and fresh on a genuine second event
    /// about the same entity.
    /// </remarks>
    public async Task SendAsync(IEnumerable<EventType> eventTypes, Guid entityId)
    {
        var occurredUtc = dateTimeProvider.Now;
        var rows = eventTypes
            .Select(eventType => new OutboxEvent
            {
                EventId = guidFactory.Create(),
                EventType = eventType,
                EntityId = entityId,
                OccurredUtc = occurredUtc,
            })
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        dbContext.OutboxEvents.AddRange(rows);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            // Detached, or the rows would ride along with the next SaveChanges
            // somebody else makes on this scoped context and be published after
            // the caller was told they were lost.
            foreach (var row in rows)
            {
                dbContext.Entry(row).State = EntityState.Detached;
                MessagingMetrics.PublishFailed.Add(1,
                    new KeyValuePair<string, object?>("event", row.EventType.ToString()),
                    new KeyValuePair<string, object?>("reason", exception.GetType().Name));
            }

            logger.LogWarning(exception,
                "Failed to store {EventTypes} for {EntityId} in the outbox",
                rows.Select(row => row.EventType), entityId);
        }
    }
}
