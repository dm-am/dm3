using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Core.Tracing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// The depth, the age of the oldest pending row and the dead rows of every queue, read from
/// the outbox table each time the exporter collects.
/// </summary>
/// <remarks>
/// Computed at the scrape rather than recorded by a loop, so a hung worker cannot
/// freeze the gauge that should show it hanging. The three gauges share one query per scrape:
/// whichever gauge is asked a second time takes a new reading, so the order in which the
/// exporter calls them does not matter. A failed reading is not a number for every queue,
/// because a zero would show an outage as an empty queue, and nothing escapes a callback,
/// so every queue keeps a value on the page. A hosted service that does no work: the host
/// builds it at start, so the gauges exist from the first scrape.
/// </remarks>
internal sealed class QueueMetrics : IHostedService, IDisposable
{
    private static readonly string[] Queues = [QueueNames.Notifications, QueueNames.Mail];

    // One row per queue, also for a queue without rows. Pending is neither processed nor dead,
    // a row in retry included: a mail server down for an hour ages the mail queue.
    private const string ReadingSql =
        """
        SELECT q.queue,
               count(o."Id") FILTER (WHERE o."DeadUtc" IS NULL)::double precision,
               coalesce(extract(epoch FROM $1 - min(o."OccurredUtc") FILTER (WHERE o."DeadUtc" IS NULL)), 0)::double precision,
               count(o."Id") FILTER (WHERE o."DeadUtc" IS NOT NULL)::double precision
        FROM unnest($2::text[]) AS q(queue)
        LEFT JOIN "OutboxEvents" o ON o."Queue" = q.queue AND o."ProcessedUtc" IS NULL
        GROUP BY q.queue
        """;

    // The whole reading, the wait for a connection included, inside the ten seconds Prometheus
    // waits for a scrape: a database that does not answer reads not-a-number, not a dead API.
    private static readonly TimeSpan ReadingBudget = TimeSpan.FromSeconds(5);

    private readonly NpgsqlDataSource _dataSource;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<QueueMetrics> _logger;
    private readonly Meter _meter = new(MessagingMetrics.MeterName, DmActivitySource.Version);
    private readonly Lock _gate = new();
    private readonly HashSet<string> _readFromCurrent = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, Reading>? _current;
    private bool _failing;

    public QueueMetrics(NpgsqlDataSource dataSource, IDateTimeProvider clock, ILogger<QueueMetrics> logger)
    {
        _dataSource = dataSource;
        _clock = clock;
        _logger = logger;

        _meter.CreateObservableGauge<double>("dm.messaging.outbox_pending",
            () => Observe("pending", reading => reading.Pending), null,
            "Outbox rows neither processed nor dead");
        _meter.CreateObservableGauge<double>("dm.messaging.outbox_lag",
            () => Observe("age", reading => reading.Age), "s",
            "Age of the oldest outbox row neither processed nor dead");
        _meter.CreateObservableGauge<double>("dm.messaging.outbox_dead",
            () => Observe("dead", reading => reading.Dead), null,
            "Dead outbox rows");
    }

    private IEnumerable<Measurement<double>> Observe(string gauge, Func<Reading, double> value)
    {
        IReadOnlyDictionary<string, Reading> readings;
        lock (_gate)
        {
            if (_current is null || !_readFromCurrent.Add(gauge))
            {
                _current = Read();
                _readFromCurrent.Clear();
                _readFromCurrent.Add(gauge);
            }

            readings = _current;
        }

        return Queues.Select(queue => new Measurement<double>(
            readings.TryGetValue(queue, out var reading) ? value(reading) : double.NaN,
            MessagingMetrics.Queue(queue)));
    }

    private IReadOnlyDictionary<string, Reading> Read()
    {
        try
        {
            // The callback is synchronous; the token ends the reading, the wait bounds the scrape
            // even where the driver does not hear the token.
            using var budget = new CancellationTokenSource(ReadingBudget);
            var readings = ReadAsync(budget.Token).WaitAsync(ReadingBudget).GetAwaiter().GetResult();
            if (_failing)
            {
                _failing = false;
                _logger.LogInformation("The queue gauges read the outbox table again");
            }

            return readings;
        }
        catch (Exception exception)
        {
            // Unknown, not empty: every queue reads not-a-number until the next scrape.
            // Said once when the outage starts, not on every scrape it lasts.
            if (!_failing)
            {
                _failing = true;
                _logger.LogWarning(exception,
                    "The queue gauges cannot read the outbox table and read not-a-number until it answers");
            }

            return new Dictionary<string, Reading>();
        }
    }

    private async Task<Dictionary<string, Reading>> ReadAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(ReadingSql, connection);
        command.Parameters.Add(new NpgsqlParameter { Value = _clock.Now.ToUniversalTime() });
        command.Parameters.Add(new NpgsqlParameter { Value = Queues });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var readings = new Dictionary<string, Reading>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            readings[reader.GetString(0)] = new Reading(reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3));
        }

        return readings;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _meter.Dispose();

    private sealed record Reading(double Pending, double Age, double Dead);
}
