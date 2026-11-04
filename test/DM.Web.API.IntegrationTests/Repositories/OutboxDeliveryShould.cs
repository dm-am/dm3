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
/// The whole outbox with the broker in the loop: the real relay, the real confirm producer, a queue bound to
/// dm.events, and a broker container that goes down and comes back.
/// </summary>
/// <remarks>
/// After the broker returns, the backlog leaves in order while the lag metric rises and falls back; the happy path
/// pins one relay tick plus the batch, bounded at five seconds.
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
                "the happy-path latency is one relay tick plus the batch");

            // The batch commits its marks after the counts, so a consumer can see the message first.
            await WaitUntil(() =>
                {
                    using var check = DatabaseFixture.Factory.Services.CreateScope();
                    return Context(check).OutboxEvents.Single(row => row.EventId == eventId).PublishedUtc != null;
                },
                TimeSpan.FromSeconds(5),
                "the confirm arrived, so the row is done once its batch commits");
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

            var (entityId, stored) = await StoreDuringOutageAsync();

            await WaitUntil(() => Interlocked.Read(ref publishFailures) > 0,
                TimeSpan.FromSeconds(60),
                "the relay keeps trying against the dead broker and counts every refusal");

            await DatabaseFixture.StartBrokerAsync();

            var delivered = await Consume(queue, stored, TimeSpan.FromSeconds(120));
            delivered.Should().Contain(stored,
                "everything said while the broker was down arrives after it is back - the " +
                "loss this wave closes");

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
                "the lag returns to zero without anybody restarting the API");
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None);
            relay.Dispose();
        }
    }

    /// <summary>
    /// The outage itself: the broker goes down, three events are said while it is
    /// gone, and the rows the outbox holds for them are read back.
    /// </summary>
    /// <returns>The entity the events were about and their event ids, in the order they were said.</returns>
    private async Task<(Guid EntityId, Guid[] Stored)> StoreDuringOutageAsync()
    {
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
                "a dead broker must not fail the requests whose work is already done");
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
        return (entityId, stored);
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
    /// Polls the queue with a fresh connection per round until every id arrived or the budget ran out.
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
    /// Reads the outbox instruments off the meter: the gauges are what the alerts consume.
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
