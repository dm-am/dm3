using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.RelationalStorage;

/// <summary>
/// Publishing a domain event is an INSERT into the outbox, and the contract of
/// SendAsync survives the change word for word.
/// </summary>
/// <remarks>
/// The successor of EventPublishingShould, over rows instead of a transport
/// mock. What the old suite held - a refusal is swallowed, logged and counted,
/// and every publication leaves with a fresh identity of its own - still holds
/// here; what is new is the row itself: every field the relay will need has to
/// be in it, because the wire message is reconstructed from the row alone.
/// </remarks>
public class OutboxEventProducerShould
{
    [Fact]
    public async Task StoreARowTheRelayCanRebuildTheMessageFrom()
    {
        await using var context = Context();
        var producer = Producer(context, out var clock, out _);
        var entityId = Guid.NewGuid();

        await producer.SendAsync(EventType.NewGame, entityId);

        var row = context.OutboxEvents.Single();
        row.EventType.Should().Be(EventType.NewGame);
        row.EntityId.Should().Be(entityId);
        row.EventId.Should().NotBe(Guid.Empty,
            "an empty id reads to the consumer as a pre-EventId message and is never deduplicated");
        row.OccurredUtc.Should().Be(clock.Now, "the order of publication is the order of insertion");
        row.PublishedUtc.Should().BeNull("only the broker's confirm may ever set this");
        row.Attempts.Should().Be(0);
        row.LastError.Should().BeNull();
    }

    [Fact]
    public async Task StampAFreshEventIdOnEveryPublication()
    {
        await using var context = Context();
        var producer = Producer(context, out _, out _);
        var entityId = Guid.NewGuid();

        await producer.SendAsync(EventType.NewGame, entityId);
        await producer.SendAsync(EventType.NewGame, entityId);

        var ids = context.OutboxEvents.Select(row => row.EventId).ToList();
        ids.Should().HaveCount(2);
        ids.Distinct().Should().HaveCount(2,
            "two publications about one entity are two events, not a replay of one - " +
            "a replay is the relay re-publishing one row, which carries the stored id");
    }

    [Fact]
    public async Task StoreABatchWithOneCommit()
    {
        await using var context = Context();
        var producer = Producer(context, out _, out _);
        var entityId = Guid.NewGuid();

        await producer.SendAsync([EventType.NewGame, EventType.ChangedGame], entityId);

        context.SaveCalls.Should().Be(1,
            "the batch is atomic - all events of the call or none, unlike the N " +
            "independent publications this replaced");
        context.OutboxEvents.Select(row => row.EventType).Should().Equal(
            [EventType.NewGame, EventType.ChangedGame]);
        context.OutboxEvents.Select(row => row.EventId).Distinct().Should().HaveCount(2,
            "each event of a batch is its own publication with its own identity");
    }

    [Fact]
    public async Task SwallowAnInsertRefusal()
    {
        await using var context = Context(failing: true);
        var producer = Producer(context, out _, out var logger);

        var publish = async () => await producer.SendAsync(EventType.NewGame, Guid.NewGuid());

        await publish.Should().NotThrowAsync(
            "the write that produced the event is committed by now, and a throw would " +
            "turn it into a 500 and a duplicate on the caller's retry");
        logger.At(LogLevel.Warning).Should().HaveCount(1,
            "swallowing without a trace turns a refusing database into a quiet site; " +
            "the counter behind the alert is MessagingMetrics.PublishFailed");
    }

    [Fact]
    public async Task DetachWhatItCouldNotStore()
    {
        await using var context = Context(failing: true);
        var producer = Producer(context, out _, out _);

        await producer.SendAsync(EventType.NewGame, Guid.NewGuid());

        context.FailSaves = false;
        await context.SaveChangesAsync();

        context.OutboxEvents.Should().BeEmpty(
            "a row left in the tracker would ride along with the next commit somebody " +
            "else makes on this scoped context - published after the caller was told it " +
            "was lost");
    }

    private static OutboxEventProducer Producer(
        CountingDbContext context,
        out FixedClock clock,
        out RecordingLogger<OutboxEventProducer> logger)
    {
        clock = new FixedClock();
        logger = new RecordingLogger<OutboxEventProducer>();
        return new OutboxEventProducer(context, new SequentialGuids(), clock, logger);
    }

    private static CountingDbContext Context(bool failing = false) => new(
        new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options)
    {
        FailSaves = failing,
    };

    /// <summary>
    /// The context under the producer, counting commits and refusing them on
    /// demand: what the batch assertion and the degradation path need, and a
    /// mocked DbSet could not answer for.
    /// </summary>
    private sealed class CountingDbContext(DbContextOptions options) : DmDbContext(options)
    {
        public int SaveCalls { get; private set; }

        public bool FailSaves { get; set; }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return FailSaves
                ? Task.FromException<int>(new InvalidOperationException("the pool is exhausted"))
                : base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTimeOffset Now { get; } = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class SequentialGuids : IGuidFactory
    {
        public Guid Create() => Guid.NewGuid();
    }
}
