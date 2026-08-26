using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Persistence;
using DM.Web.API.HostedServices;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The whole outbox with the broker in the loop: the real relay service, the
/// real confirm producer, a queue bound to dm.events - and a broker container
/// that goes down and comes back, which is the deployment window the outbox
/// exists for.
/// </summary>
/// <remarks>
/// The main acceptance of the wave is the restart test (AC-1): before the
/// outbox, every event of the window was lost in silence; now SendAsync keeps
/// answering success, the rows keep accumulating, and the backlog leaves in
/// order once the broker is back - with the lag metric rising while it waits
/// and returning to zero without anybody restarting the API (AC-5). The happy
/// path pins the latency the design promises: one relay tick plus the batch
/// (AC-4, bounded at five seconds in a test).
/// </remarks>
public class OutboxDeliveryShould : IntegrationTestBase
{
    public OutboxDeliveryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task DeliverWithinATickOfTheRelay()
    {
        await ClearOutbox();
        var queue = await BoundQueue();
        var relay = Relay();
        try
        {
            await relay.StartAsync(CancellationToken.None);

            Guid eventId;
            var entityId = Guid.NewGuid();
            var watch = Stopwatch.StartNew();
            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                var producer = scope.ServiceProvider.GetRequiredService<IEventProducer>();
                await producer.SendAsync(EventType.NewGame, entityId);
                // By the entity, not Single() over the table: the shared test
                // host runs periodic jobs that store events of their own.
                eventId = Context(scope).OutboxEvents.Single(row => row.EntityId == entityId).EventId;
            }

            var delivered = await Consume(queue, [eventId], TimeSpan.FromSeconds(30));
            watch.Stop();

            delivered.Should().Contain(eventId, "the stored event is what the relay publishes");
            watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
                "the happy-path latency is one relay tick plus the batch (AC-4)");

            using var check = DatabaseFixture.Factory.Services.CreateScope();
            Context(check).OutboxEvents.Single(row => row.EventId == eventId)
                .PublishedUtc.Should().NotBeNull("the confirm arrived, so the row is done");
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None);
            relay.Dispose();
        }
    }

    [Fact]
    public async Task DeliverEverythingStoredWhileTheBrokerWasDown()
    {
        await ClearOutbox();
        var queue = await BoundQueue();

        var publishFailures = 0L;
        var lagSamples = new List<double>();
        using var listener = Listen(
            onPublishFailed: value => Interlocked.Add(ref publishFailures, value),
            onLag: value =>
            {
                lock (lagSamples)
                {
                    lagSamples.Add(value);
                }
            });

        var relay = Relay();
        try
        {
            await relay.StartAsync(CancellationToken.None);
            await DatabaseFixture.StopBrokerAsync();

            var entityId = Guid.NewGuid();
            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                var producer = scope.ServiceProvider.GetRequiredService<IEventProducer>();
                var send = async () =>
                {
                    await producer.SendAsync(EventType.NewGame, entityId);
                    await producer.SendAsync([EventType.ChangedGame, EventType.NewPublication], entityId);
                };
                await send.Should().NotThrowAsync(
                    "a dead broker must not fail the requests whose work is already done (AC-1)");
            }

            Guid[] stored;
            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                // This test's rows only: the periodic jobs of the shared test
                // host store events of their own into the same table.
                stored = Context(scope).OutboxEvents
                    .Where(row => row.EntityId == entityId)
                    .OrderBy(row => row.OccurredUtc).ThenBy(row => row.Id)
                    .Select(row => row.EventId)
                    .ToArray();
            }

            stored.Should().HaveCount(3, "every event of the outage is a row, not a loss");

            await WaitUntil(() => Interlocked.Read(ref publishFailures) > 0,
                TimeSpan.FromSeconds(60),
                "the relay keeps trying against the dead broker and counts every refusal (AC-5)");

            await DatabaseFixture.StartBrokerAsync();

            var delivered = await Consume(queue, stored, TimeSpan.FromSeconds(120));
            delivered.Should().Contain(stored,
                "everything said while the broker was down arrives after it is back - the " +
                "loss this wave closes (AC-1)");

            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                (await Context(scope).OutboxEvents
                        .CountAsync(row => row.EntityId == entityId && row.PublishedUtc == null))
                    .Should().Be(0, "the backlog drained whole");
            }

            lock (lagSamples)
            {
                lagSamples.Should().Contain(sample => sample > 0,
                    "the age of the backlog is what the alert watches while the broker is down");
            }

            await WaitUntil(() =>
                {
                    lock (lagSamples)
                    {
                        return lagSamples.Count > 0 && lagSamples[^1] == 0;
                    }
                },
                TimeSpan.FromSeconds(30),
                "the lag returns to zero without anybody restarting the API (AC-5)");
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None);
            relay.Dispose();
        }
    }

    /// <summary>
    /// The relay exactly as the host runs it - the periodic service over the
    /// factory's own container, producer and confirm timeout included.
    /// </summary>
    private OutboxRelayService Relay()
    {
        var services = DatabaseFixture.Factory.Services;
        return new OutboxRelayService(
            services,
            services.GetRequiredService<ILogger<OutboxRelayService>>(),
            services.GetRequiredService<IDmProducerBuilder>(),
            services.GetRequiredService<IOptions<RabbitMqConfiguration>>());
    }

    /// <summary>
    /// A durable queue of this test bound to everything dm.events routes: it
    /// survives the broker restart the same way the workers' queues do.
    /// </summary>
    private async Task<string> BoundQueue()
    {
        var queue = $"dm.test.outbox.{Guid.NewGuid():N}";
        await using var connection = await Connect();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync("dm.events", ExchangeType.Topic, durable: true, autoDelete: false);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queue, "dm.events", "#");
        return queue;
    }

    /// <summary>
    /// Polls the queue until every expected id arrived or the budget ran out.
    /// A fresh connection per poll round, because the point of these tests is a
    /// broker that was down a moment ago.
    /// </summary>
    private async Task<HashSet<Guid>> Consume(string queue, Guid[] expected, TimeSpan budget)
    {
        var delivered = new HashSet<Guid>();
        var deadline = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var connection = await Connect();
                await using var channel = await connection.CreateChannelAsync();
                while (await channel.BasicGetAsync(queue, autoAck: true) is { } got)
                {
                    using var body = JsonDocument.Parse(got.Body.ToArray());
                    delivered.Add(body.RootElement.GetProperty("eventId").GetGuid());
                }

                if (expected.All(delivered.Contains))
                {
                    return delivered;
                }
            }
            catch
            {
                // The broker is not back yet; the deadline is the judge.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return delivered;
    }

    private async Task<IConnection> Connect()
    {
        var configuration = DatabaseFixture.Factory.Services
            .GetRequiredService<IOptions<RabbitMqConfiguration>>().Value;
        return await configuration.CreateConnectionFactory().CreateConnectionAsync();
    }

    /// <summary>
    /// Reads the outbox instruments off the meter, because the gauges the relay
    /// updates are the interface the alerts consume and a test that never
    /// listens to them holds nothing.
    /// </summary>
    private static MeterListener Listen(Action<long> onPublishFailed, Action<double> onLag)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name != "DM.Messaging")
                {
                    return;
                }

                if (instrument.Name is "dm.messaging.outbox_publish_failed" or "dm.messaging.outbox_lag")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (instrument.Name == "dm.messaging.outbox_publish_failed")
            {
                onPublishFailed(value);
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
        {
            if (instrument.Name == "dm.messaging.outbox_lag")
            {
                onLag(value);
            }
        });
        listener.Start();
        return listener;
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan budget, string because)
    {
        var deadline = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        condition().Should().BeTrue(because);
    }

    private async Task ClearOutbox()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        await Context(scope).OutboxEvents.ExecuteDeleteAsync();
    }

    private static DmDbContext Context(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<DmDbContext>();
}
