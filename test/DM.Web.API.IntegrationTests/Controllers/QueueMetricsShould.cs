using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Retention;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers;

/// <summary>
/// The depth, the age and the dead rows of every queue, as the metrics page of the API shows
/// them to Prometheus.
/// </summary>
/// <remarks>
/// Rows go in as rows of the outbox table, the numbers come out of the scrape. No worker runs in
/// the test host, so nothing but the test touches the rows. The unreachable
/// database is QueueMetricsShould's of the persistence tests.
/// </remarks>
public class QueueMetricsShould : IntegrationTestBase
{
    private static readonly string[] Queues = [QueueNames.Notifications, QueueNames.Mail];

    public QueueMetricsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ReadZeroOnEverySeriesOfAnEmptyQueue()
    {
        await ClearOutbox();

        var scrape = await Scrape();

        foreach (var queue in Queues)
        {
            scrape.Should().Contain(("dm_messaging_outbox_pending", queue, 0),
                "an empty queue is a zero, not a missing series that a rule reads as nothing");
            scrape.Should().Contain(("dm_messaging_outbox_lag_seconds", queue, 0));
            scrape.Should().Contain(("dm_messaging_outbox_dead", queue, 0),
                "a queue without dead rows reads zero dead");
        }
    }

    [Fact]
    public async Task AgeTheQueueByItsOldestPendingRowAndOnlyWhileItWaits()
    {
        await ClearOutbox();
        // Processed long ago: a gauge over all rows would read its age instead.
        await Insert(Row(QueueNames.Notifications, secondsAgo: 3600, processed: true));
        var waiting = Row(QueueNames.Notifications, secondsAgo: 400);
        await Insert(waiting);

        var before = await Scrape();

        Value(before, "dm_messaging_outbox_lag_seconds", QueueNames.Notifications)
            .Should().BeInRange(400, 3599,
                "the age is the oldest row that waits, and a processed row waits for nothing");
        Value(before, "dm_messaging_outbox_pending", QueueNames.Notifications).Should().Be(1);
        Value(before, "dm_messaging_outbox_lag_seconds", QueueNames.Mail).Should().Be(0,
            "each queue is aged by its own rows");

        await MarkProcessed(waiting.EventId);
        var after = await Scrape();

        Value(after, "dm_messaging_outbox_lag_seconds", QueueNames.Notifications).Should().Be(0,
            "a drained queue is not old, or the backlog alert stays on after the worker caught up");
        Value(after, "dm_messaging_outbox_pending", QueueNames.Notifications).Should().Be(0);
    }

    [Fact]
    public async Task CountDeadRowsApartFromPendingOnes()
    {
        await ClearOutbox();
        var dead = Row(QueueNames.Mail, secondsAgo: 7200);
        dead.DeadUtc = DateTimeOffset.UtcNow.AddSeconds(-60);
        await Insert(dead);

        var before = await Scrape();

        Value(before, "dm_messaging_outbox_dead", QueueNames.Mail).Should().Be(1);
        Value(before, "dm_messaging_outbox_pending", QueueNames.Mail).Should().Be(0,
            "a dead row waits for a human, not for the worker");
        Value(before, "dm_messaging_outbox_lag_seconds", QueueNames.Mail).Should().Be(0,
            "a dead row is the dead-row alert's, and aging the queue by it would page twice");

        await ClearOutbox();
        var after = await Scrape();

        Value(after, "dm_messaging_outbox_dead", QueueNames.Mail).Should().Be(0,
            "once the dead row is dealt with the dead-row alert has to clear");
    }

    [Fact]
    public async Task FollowADeadRowUntilItIsReturnedOrDeleted()
    {
        await ClearOutbox();
        var returned = Row(QueueNames.Mail, secondsAgo: 7200);
        returned.Attempts = 20;
        returned.DeadUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expired = Row(QueueNames.Notifications, secondsAgo: 7200);
        expired.Attempts = 20;
        expired.DeadUtc = DateTimeOffset.UtcNow.AddDays(-31);
        await Insert(returned);
        await Insert(expired);

        var before = await Scrape();

        Value(before, "dm_messaging_outbox_dead", QueueNames.Mail).Should().Be(1);
        Value(before, "dm_messaging_outbox_dead", QueueNames.Notifications).Should().Be(1);

        using (var scope = DatabaseFixture.Factory.Services.CreateScope())
        {
            (await Context(scope).Database.ExecuteSqlRawAsync(DeadRows.ReturnCommand(returned.Id))).Should().Be(1);
            await scope.ServiceProvider.GetRequiredService<IRetentionSweepProcessor>().SweepAsync();
        }

        var after = await Scrape();

        Value(after, "dm_messaging_outbox_dead", QueueNames.Mail).Should().Be(0,
            "a returned row is alive again, so the dead-row alert clears");
        Value(after, "dm_messaging_outbox_pending", QueueNames.Mail).Should().Be(1, "and it waits for the worker");
        Value(after, "dm_messaging_outbox_dead", QueueNames.Notifications).Should().Be(0,
            "a dead row the retention deleted is not dead any more");
    }

    // The exporter answers a scrape within 300 ms of the previous one from its cache.
    private async Task<List<(string Series, string Queue, double Value)>> Scrape()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400));
        var page = await Client.GetStringAsync("/metrics");
        return SeriesLine.Matches(page)
            .Select(match => (
                match.Groups["series"].Value,
                match.Groups["queue"].Value,
                double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static double Value(
        IEnumerable<(string Series, string Queue, double Value)> scrape, string series, string queue) =>
        scrape.Single(sample => sample.Series == series && sample.Queue == queue).Value;

    private static readonly Regex SeriesLine = new(
        @"^(?<series>dm_messaging_outbox_(?:pending|lag_seconds|dead))\{[^}]*queue=""(?<queue>[^""]+)""[^}]*\}\s+(?<value>\S+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // Other tests of this collection publish through the API and leave rows behind.
    private async Task ClearOutbox()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        await Context(scope).OutboxEvents.ExecuteDeleteAsync();
    }

    private async Task Insert(OutboxEvent row)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var context = Context(scope);
        context.OutboxEvents.Add(row);
        await context.SaveChangesAsync();
    }

    private async Task MarkProcessed(Guid eventId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        await Context(scope).OutboxEvents
            .Where(row => row.EventId == eventId)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.ProcessedUtc, DateTimeOffset.UtcNow));
    }

    private static DmDbContext Context(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<DmDbContext>();

    private static OutboxEvent Row(string queue, int secondsAgo, bool processed = false) => new()
    {
        EventId = Guid.NewGuid(),
        Queue = queue,
        EventType = queue == QueueNames.Notifications ? EventType.NewGame : null,
        EntityId = queue == QueueNames.Notifications ? Guid.NewGuid() : null,
        Payload = queue == QueueNames.Mail ? "{}" : null,
        OccurredUtc = DateTimeOffset.UtcNow.AddSeconds(-secondsAgo),
        ProcessedUtc = processed ? DateTimeOffset.UtcNow.AddSeconds(-secondsAgo / 2) : null,
    };
}
