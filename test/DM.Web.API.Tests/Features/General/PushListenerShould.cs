using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Tracing;
using DM.Testing;
using DM.Web.API.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <remarks>
/// A database that does not answer is the listener's ordinary weather: it keeps attempting on
/// its ladder, says degraded meanwhile and never takes the host down with it. Against a closed
/// port, so no database is needed; the reconnect after a lost connection is in the integration
/// tests.
/// </remarks>
public class PushListenerShould : UnitTestBase
{
    private const string ClosedPort = "Host=127.0.0.1;Port=1;Username=nobody;Password=none;Timeout=1";

    private const int AttemptsBeforeStop = 5;

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

    private readonly PushListenerState _state = new();

    // Mutation it must fail on: no catch around one attempt, which ends the loop and stops the host.
    [Fact]
    public async Task KeepAttemptingOnItsLadderWithoutStoppingTheHost()
    {
        using var stopping = new CancellationTokenSource(TestTimeout);
        var waited = new List<int>();

        using var listener = Listener(attempt =>
        {
            waited.Add(attempt);
            if (attempt == AttemptsBeforeStop)
            {
                stopping.Cancel();
            }

            return TimeSpan.Zero;
        });

        await listener.StartAsync(stopping.Token);
        var running = async () => await listener.ExecuteTask!.WaitAsync(TestTimeout);

        await running.Should().NotThrowAsync("an exception out of a hosted service stops the whole host");
        waited.Should().Equal([1, 2, 3, 4, 5],
            "each failed attempt waits the next step of the ladder, and the stop ends the waiting");
        (await _state.CheckHealthAsync(new HealthCheckContext())).Status.Should().Be(HealthStatus.Degraded,
            "an instance that never reached its database hears no push");
    }

    /// <remarks>
    /// The delivery is not started, so the buffer never drains and the push past its capacity is
    /// the one dropped. Mutation it must fail on: a drop that is not counted.
    /// </remarks>
    [Fact]
    public void CountThePushDroppedFromAFullBuffer()
    {
        using var listener = Listener(_ => TimeSpan.Zero);
        var piece = PushPayload.Cut(new RealtimeNotification
        {
            NotificationId = Guid.NewGuid(),
            EventType = EventType.GlobalChatEventEnded,
            Metadata = new { },
            RecipientIds = [Guid.NewGuid()],
        }).Single();
        var dropped = 0;
        using var counter = new MeterListener
        {
            InstrumentPublished = (instrument, self) =>
            {
                if (instrument.Meter.Name == MessagingMetrics.MeterName &&
                    instrument.Name == "dm.messaging.delivery_failed")
                {
                    self.EnableMeasurementEvents(instrument);
                }
            },
        };
        counter.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var ofThisTest = false;
            foreach (var tag in tags)
            {
                ofThisTest |= tag.Key == "event" && Equals(tag.Value, nameof(EventType.GlobalChatEventEnded));
            }

            if (ofThisTest)
            {
                Interlocked.Add(ref dropped, (int)value);
            }
        });
        counter.Start();

        for (var heard = 0; heard <= PushListener.BufferCapacity; heard++)
        {
            listener.Hear(piece);
        }

        dropped.Should().Be(1, "only the push past the buffer's capacity is dropped, and every drop is counted");
    }

    private PushListener Listener(Func<int, TimeSpan> ladder) => new(
        NullLogger<PushListener>.Instance,
        Options.Create(new ConnectionStrings { Rdb = ClosedPort }),
        _state,
        new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
        ladder);
}
