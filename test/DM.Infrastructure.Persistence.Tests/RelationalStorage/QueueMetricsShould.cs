using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using AwesomeAssertions;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Testing;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.RelationalStorage;

/// <remarks>
/// Observed the way the exporter observes: a listener asks the meter for its gauges, as a scrape
/// does. The rows-in, numbers-out half needs a database and is QueueMetricsShould's of the
/// integration tier.
/// </remarks>
public class QueueMetricsShould
{
    /// <remarks>Mutation it must fail on: a callback that does not catch, which loses every series.</remarks>
    [Fact]
    public void ReadNotANumberForEveryQueueWhenTheDatabaseIsUnreachable()
    {
        // Nothing listens on port 1, so every connection is refused.
        using var source = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=dm3;Username=dm_app;Password=unused;Timeout=1");
        using var metrics = new QueueMetrics(source, new FixedClock(DateTimeOffset.UtcNow), new RecordingLogger<QueueMetrics>());

        var observed = Scrape();

        AssertNotANumberForEveryQueue(observed);
    }

    /// <remarks>
    /// Mutation it must fail on: the reading without its budget, which waits out the driver's
    /// connection timeout of 15 seconds and holds the scrape past the one Prometheus gives it.
    /// </remarks>
    [Fact]
    public void AnswerNotANumberWithinItsBudgetWhenTheDatabaseDoesNotAnswer()
    {
        // A port that takes the connection and never says a word, like a host that drops packets.
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        var port = ((IPEndPoint)silent.LocalEndpoint).Port;
        using var source = NpgsqlDataSource.Create(
            $"Host=127.0.0.1;Port={port};Database=dm3;Username=dm_app;Password=unused;Pooling=false");
        using var metrics = new QueueMetrics(source, new FixedClock(DateTimeOffset.UtcNow), new RecordingLogger<QueueMetrics>());

        var watch = Stopwatch.StartNew();
        var observed = Scrape();

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(8),
            "the reading has five seconds in all, inside the ten seconds Prometheus waits for a scrape");
        AssertNotANumberForEveryQueue(observed);
    }

    /// <remarks>Mutation it must fail on: a catch that logs nothing, or logs on every scrape.</remarks>
    [Fact]
    public void WarnOnceWhenTheReadingStartsFailing()
    {
        using var source = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=dm3;Username=dm_app;Password=unused;Timeout=1");
        var logger = new RecordingLogger<QueueMetrics>();
        using var metrics = new QueueMetrics(source, new FixedClock(DateTimeOffset.UtcNow), logger);

        Scrape();
        Scrape();

        logger.At(LogLevel.Warning).Should().ContainSingle(
            "an outage is said once when it starts, not on every scrape it lasts");
    }

    private static void AssertNotANumberForEveryQueue(List<(string Series, string? Queue, double Value)> observed)
    {
        observed.Select(measurement => (measurement.Series, measurement.Queue))
            .Should().BeEquivalentTo(
                [
                    ("dm.messaging.outbox_pending", "dm.notifications"),
                    ("dm.messaging.outbox_pending", "dm.mail.sending"),
                    ("dm.messaging.outbox_lag", "dm.notifications"),
                    ("dm.messaging.outbox_lag", "dm.mail.sending"),
                    ("dm.messaging.outbox_dead", "dm.notifications"),
                    ("dm.messaging.outbox_dead", "dm.mail.sending"),
                ],
                "every queue keeps its series through an outage, or the scrape drops them and " +
                "the dashboard shows a gap where it should show the outage");
        observed.Should().OnlyContain(measurement => double.IsNaN(measurement.Value),
            "an unreachable database must read as unknown, not as an empty queue");
    }

    private static List<(string Series, string? Queue, double Value)> Scrape()
    {
        var observed = new List<(string Series, string? Queue, double Value)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, self) =>
            {
                if (instrument.Name.StartsWith("dm.messaging.outbox_", StringComparison.Ordinal) &&
                    instrument.IsObservable)
                {
                    self.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            string? queue = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "queue")
                {
                    queue = tag.Value?.ToString();
                }
            }

            observed.Add((instrument.Name, queue, value));
        });
        listener.Start();

        listener.RecordObservableInstruments();
        return observed;
    }
}
