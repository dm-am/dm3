using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Core.Tracing;
using DM.Infrastructure.Persistence.Entities.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Event producer implementation that stores domain events in the outbox table
/// for the notification worker to claim.
/// </summary>
/// <remarks>
/// The insert is a separate commit of the scoped context after the domain one, so a
/// worker restart delays delivery instead of losing it. It flushes the whole change
/// tracker, so it must run after the domain commit.
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
    /// A refusal is logged and counted behind the EventsNotPublished alert, never thrown:
    /// the write is already committed, and a 500 would make the caller repeat it. Each
    /// row gets its own EventId, by which the worker deduplicates a second handling of the row.
    /// </remarks>
    public async Task SendAsync(IEnumerable<EventType> eventTypes, Guid entityId)
    {
        var occurredUtc = dateTimeProvider.Now;
        var traceParent = StoredTraceParent.Current();
        var rows = eventTypes
            .Select(eventType => new OutboxEvent
            {
                EventId = guidFactory.Create(),
                Queue = QueueNames.Notifications,
                TraceParent = traceParent,
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
