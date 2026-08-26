using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Outbox;
using DbNotification = DM.Infrastructure.Persistence.Entities.Personal.Notifications.Notification;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The claim-and-mark mechanics of the outbox relay processor, against a live
/// Postgres because the claim is raw SQL with FOR UPDATE SKIP LOCKED and
/// nothing else executes it.
/// </summary>
/// <remarks>
/// The delegate here is a recorder or a saboteur, never a broker: what these
/// tests hold is the storage half of at-least-once delivery - the order
/// (INV-3, AC-3), the stop on the first refusal with only the confirmed prefix
/// marked (INV-1, INV-2), the identity a republication carries (INV-8, AC-2)
/// and the lock that keeps two concurrent relays off one row (D9). The broker
/// half lives in OutboxDeliveryShould.
/// </remarks>
public class OutboxRelayShould : IntegrationTestBase
{
    public OutboxRelayShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task PublishInOccurredOrderAndOneBatchAtMost()
    {
        using var scope = await ClearedScope();
        var context = Context(scope);

        // Inserted out of order on purpose: the claim orders, not the insert.
        var second = Row(EventType.NewGame, minutesAgo: 20);
        var third = Row(EventType.ChangedGame, minutesAgo: 10);
        var first = Row(EventType.NewPublication, minutesAgo: 30);
        context.OutboxEvents.AddRange(second, third, first);
        var overflow = Enumerable.Range(0, IOutboxRelayProcessor.BatchSize - 2)
            .Select(_ => Row(EventType.NewMessage, minutesAgo: 5))
            .ToList();
        context.OutboxEvents.AddRange(overflow);
        await context.SaveChangesAsync();

        var published = new List<OutboxEnvelope>();
        var result = await Processor(scope).RelayBatchAsync(
            (envelope, _) =>
            {
                published.Add(envelope);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        result.Faulted.Should().BeFalse();
        result.Published.Should().Be(IOutboxRelayProcessor.BatchSize,
            "the claim takes one batch and no more, however deep the backlog");
        // At least, not exactly: the shared test host runs periodic jobs that
        // publish events of their own, and one may land a row mid-test.
        result.Pending.Should().BeGreaterThanOrEqualTo(1,
            "the overflow row waits for the next pass");
        published.Take(3).Select(e => e.EventId).Should().Equal(
            [first.EventId, second.EventId, third.EventId],
            "the consumer is sequential, so the relay publishes oldest first (AC-3)");
        result.OldestPendingAge.Should().BePositive(
            "the age of the leftover row is what the backlog alert is calibrated on");

        var mine = overflow.Select(row => row.EventId)
            .Concat([first.EventId, second.EventId, third.EventId])
            .ToList();
        using var check = DatabaseFixture.Factory.Services.CreateScope();
        (await Context(check).OutboxEvents
                .CountAsync(row => mine.Contains(row.EventId) && row.PublishedUtc == null))
            .Should().Be(1, "exactly one of these rows is past the batch limit and untouched");
    }

    [Fact]
    public async Task StopOnTheFirstRefusalAndMarkOnlyThePrefix()
    {
        using var scope = await ClearedScope();
        var context = Context(scope);
        var rows = Enumerable.Range(0, 5)
            .Select(i => Row(EventType.NewGame, minutesAgo: 50 - i))
            .ToList();
        context.OutboxEvents.AddRange(rows);
        await context.SaveChangesAsync();

        var calls = 0;
        var result = await Processor(scope).RelayBatchAsync(
            (_, _) => ++calls == 3
                ? Task.FromException(new TimeoutException("the broker did not confirm"))
                : Task.CompletedTask,
            CancellationToken.None);

        result.Published.Should().Be(2);
        result.Faulted.Should().BeTrue();
        result.Pending.Should().BeGreaterThanOrEqualTo(3,
            "the refused row and the untouched tail are all still owed");
        calls.Should().Be(3, "the batch stops on the refusal instead of skipping ahead (D7)");

        var stored = await Reload(rows);
        stored.Take(2).Should().OnlyContain(row => row.PublishedUtc != null,
            "the prefix the broker confirmed is done");
        stored.Skip(2).Should().OnlyContain(row => row.PublishedUtc == null,
            "a mark without a confirm would record delivered about a message the broker " +
            "may not have taken (INV-2)");
        stored[2].Attempts.Should().Be(1, "the refused row carries its diagnostics");
        stored[2].LastError.Should().Contain("TimeoutException",
            "the human the backlog alert calls reads the reason off the row");
        stored.Skip(3).Should().OnlyContain(row => row.Attempts == 0,
            "the tail was never attempted, so nothing about it changed");
    }

    [Fact]
    public async Task RepublishWithTheStoredEventId()
    {
        using var scope = await ClearedScope();
        var context = Context(scope);
        var row = Row(EventType.NewGame, minutesAgo: 1);
        context.OutboxEvents.Add(row);
        await context.SaveChangesAsync();

        // Only this test's row is recorded: the shared test host runs periodic
        // jobs whose own events may join the claimed batch.
        var seen = new List<Guid>();
        var processor = Processor(scope);
        await processor.RelayBatchAsync(
            (envelope, _) =>
            {
                if (envelope.EntityId == row.EntityId)
                {
                    seen.Add(envelope.EventId);
                }

                return Task.FromException(new TimeoutException("confirm lost"));
            },
            CancellationToken.None);
        var retry = await processor.RelayBatchAsync(
            (envelope, _) =>
            {
                if (envelope.EntityId == row.EntityId)
                {
                    seen.Add(envelope.EventId);
                }

                return Task.CompletedTask;
            },
            CancellationToken.None);

        retry.Published.Should().BeGreaterThanOrEqualTo(1);
        seen.Should().Equal([row.EventId, row.EventId],
            "a republication of one row is a replay of one event, and the consumer can " +
            "only know that if both publications carry the stored id (INV-8)");
    }

    /// <summary>
    /// AC-2, the far end: the unique index of W1.4 is what turns the relay's
    /// at-least-once into the reader's exactly-one notification, so a duplicate
    /// write under one (EventId, EventType) has to be refused by the schema.
    /// </summary>
    [Fact]
    public async Task LeanOnTheIndexThatSwallowsADuplicateDelivery()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var eventId = Guid.NewGuid();

        var first = Context(scope);
        first.Notifications.Add(Notification(eventId));
        await first.SaveChangesAsync();

        using var second = DatabaseFixture.CreateDbContext();
        second.Notifications.Add(Notification(eventId));
        var duplicate = async () => await second.SaveChangesAsync();

        (await duplicate.Should().ThrowAsync<DbUpdateException>(
                "one event fans out to at most one notification per output type - the " +
                "invariant the idempotent consumer write leans on"))
            .WithInnerException<Npgsql.PostgresException>()
            .Which.SqlState.Should().Be("23505");
    }

    [Fact]
    public async Task LeaveARowClaimedByAConcurrentRelayAlone()
    {
        using var scope = await ClearedScope();
        var context = Context(scope);
        var row = Row(EventType.NewGame, minutesAgo: 1);
        context.OutboxEvents.Add(row);
        await context.SaveChangesAsync();

        using var competing = DatabaseFixture.Factory.Services.CreateScope();

        var claimed = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var holder = Processor(scope).RelayBatchAsync(
            async (envelope, _) =>
            {
                if (envelope.EventId == row.EventId)
                {
                    claimed.TrySetResult();
                    await release.Task;
                }
            },
            CancellationToken.None);

        await claimed.Task;
        var contenderSaw = new List<Guid>();
        await Processor(competing).RelayBatchAsync(
            (envelope, _) =>
            {
                contenderSaw.Add(envelope.EventId);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        contenderSaw.Should().NotContain(row.EventId,
            "in the deployment window two relays run at once, and SKIP LOCKED is what " +
            "keeps the second off the rows the first is publishing (D9)");

        release.SetResult();
        (await holder).Published.Should().BeGreaterThanOrEqualTo(1);

        using var check = DatabaseFixture.Factory.Services.CreateScope();
        Context(check).OutboxEvents.Single(stored => stored.EventId == row.EventId)
            .PublishedUtc.Should().NotBeNull("the holder finished its claim and marked the row");
    }

    private async Task<IServiceScope> ClearedScope()
    {
        var scope = DatabaseFixture.Factory.Services.CreateScope();
        // Other tests of this collection publish through the API and leave rows
        // behind; a claim over their leftovers would make every order and count
        // below depend on what happened to run first.
        await Context(scope).OutboxEvents.ExecuteDeleteAsync();
        return scope;
    }

    private static DmDbContext Context(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<DmDbContext>();

    private static IOutboxRelayProcessor Processor(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IOutboxRelayProcessor>();

    private async Task<List<OutboxEvent>> Reload(List<OutboxEvent> rows)
    {
        var ids = rows.Select(row => row.Id).ToList();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        return await Context(scope).OutboxEvents
            .Where(row => ids.Contains(row.Id))
            .OrderBy(row => row.OccurredUtc).ThenBy(row => row.Id)
            .ToListAsync();
    }

    private static OutboxEvent Row(EventType eventType, int minutesAgo) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = eventType,
        EntityId = Guid.NewGuid(),
        OccurredUtc = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo),
    };

    private static DbNotification Notification(Guid eventId) => new()
    {
        NotificationId = Guid.NewGuid(),
        EventId = eventId,
        EventType = EventType.NewGame,
        CreatedUtc = DateTimeOffset.UtcNow,
        Metadata = "{}",
    };
}
