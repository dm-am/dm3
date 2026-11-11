using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using AwesomeAssertions;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Testing;
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
    [Fact]
    public void ReadNotANumberForEveryQueueWhenTheDatabaseIsUnreachable()
    {
        // Nothing listens on port 1, so every connection is refused.
        using var source = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=dm3;Username=dm_app;Password=unused;Timeout=1");
        using var metrics = new QueueMetrics(source, new FixedClock(DateTimeOffset.UtcNow));

        var observed = Scrape();

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
