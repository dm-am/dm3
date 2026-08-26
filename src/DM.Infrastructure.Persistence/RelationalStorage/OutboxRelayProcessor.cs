using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Events;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <inheritdoc />
/// <remarks>
/// The claim-and-mark half of the relay: everything about rows and locks lives
/// here, everything about the channel, the schedule and the metrics lives in
/// the host's OutboxRelayService.
///
/// FOR UPDATE SKIP LOCKED guards the deployment window when the old and the new
/// API process run their relays at once: without it both would publish the same
/// rows - which W1.4 would swallow - but worse, both would mark them. The
/// identifiers in the SQL are compile-time constants of this file, the same
/// reasoning as the retention sweeper next door.
///
/// The batch stops on the first refused row instead of skipping ahead: the
/// consumer is sequential (prefetch=1), so a skip would deliver a younger event
/// before an older one, and a publish refusal is almost always the broker being
/// down - common to every row, so skipping would not have helped anyway. The
/// refused row gets Attempts+1 and LastError for the human the backlog alert
/// calls; the confirmed prefix gets PublishedUtc; both are committed together.
///
/// A replay of the execution strategy replays the whole delegate, already made
/// publications included. Safe: every republication of a row carries the same
/// EventId, and the consumer's unique index swallows the duplicate.
/// </remarks>
internal class OutboxRelayProcessor(
    DmDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : IOutboxRelayProcessor
{
    /// <summary>
    /// The claim: pending rows in publication order, locked for this
    /// transaction, rows locked by a concurrent relay skipped.
    /// </summary>
    private static readonly string ClaimSql =
        $"""
         SELECT * FROM "OutboxEvents"
         WHERE "PublishedUtc" IS NULL
         ORDER BY "OccurredUtc", "Id"
         LIMIT {IOutboxRelayProcessor.BatchSize}
         FOR UPDATE SKIP LOCKED
         """;

    /// <inheritdoc />
    public async Task<OutboxRelayResult> RelayBatchAsync(
        Func<OutboxEnvelope, CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var batch = await dbContext.OutboxEvents
                .FromSqlRaw(ClaimSql)
                .ToListAsync(cancellationToken);

            var published = 0;
            var faulted = false;
            foreach (var row in batch)
            {
                try
                {
                    await publish(
                        new OutboxEnvelope(row.EventId, row.EventType, row.EntityId),
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The host is stopping: the claim transaction rolls back
                    // whole, and the unmarked tail goes out after the restart.
                    throw;
                }
                catch (Exception exception)
                {
                    row.Attempts += 1;
                    row.LastError = Truncate($"{exception.GetType().Name}: {exception.Message}");
                    faulted = true;
                    break;
                }

                // Only after the delegate returned, which is only after the
                // broker confirmed: a mark without the confirm would record
                // "delivered" about a message the broker may not have taken.
                row.PublishedUtc = dateTimeProvider.Now;
                published++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Counted inside the same transaction, so the rows just marked are
            // already out of the backlog this reports.
            var pending = await dbContext.OutboxEvents
                .CountAsync(row => row.PublishedUtc == null, cancellationToken);
            var oldestOccurredUtc = await dbContext.OutboxEvents
                .Where(row => row.PublishedUtc == null)
                .MinAsync(row => (DateTimeOffset?)row.OccurredUtc, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            // The drain loop runs batches in one scope: without this, every
            // full batch parks another hundred tracked rows in the context
            // until the tick ends (review of W1.5).
            dbContext.ChangeTracker.Clear();

            var oldestPendingAge = oldestOccurredUtc is { } oldest
                ? dateTimeProvider.Now - oldest
                : TimeSpan.Zero;
            return new OutboxRelayResult(published, faulted, pending, oldestPendingAge);
        });
    }

    /// <summary>
    /// LastError is a diagnostic line, not a log sink: a driver that unrolls an
    /// entire connection failure into the message must not grow the table.
    /// </summary>
    private static string Truncate(string error) =>
        error.Length <= 500 ? error : error[..500];
}
