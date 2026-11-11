using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Core.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace DM.Infrastructure.Messaging.Queues;

/// <summary>
/// One claimed outbox row, as its handler receives it.
/// </summary>
/// <param name="EventType">The event type as stored, a number: NULL on a letter.</param>
public sealed record OutboxRow(
    long Id,
    Guid EventId,
    int? EventType,
    Guid? EntityId,
    string? Payload,
    string? TraceParent);

/// <summary>
/// Handles one claimed row in a scope of its own.
/// </summary>
/// <param name="row">The claimed row.</param>
/// <param name="services">The services of the scope opened for this row alone.</param>
/// <param name="cancellationToken">The host's stopping token.</param>
/// <returns>
/// Success, or Failure when the row can never be processed: a rejection, which makes the row
/// dead at once. A throw is a failure, which spends an attempt and retries the row later.
/// </returns>
public delegate Task<ProcessResult> OutboxRowHandler(
    OutboxRow row, IServiceProvider services, CancellationToken cancellationToken);

/// <summary>
/// A worker loop that drains one queue straight from the outbox table.
/// </summary>
/// <remarks>
/// A claim is an open transaction on a connection of the loop's own: the oldest
/// ready row is locked with SKIP LOCKED, so two loops never take one row and never wait on
/// each other, and the lock stays until the handler returns, so a row of a process that
/// died is ready again at once. The handler works in a scope of its own, on other
/// connections. Nothing here goes through the data layer's retry strategy, which would
/// replay the handler with the claim.
///
/// What the handler did is written in the claim's transaction, without the stopping token, so
/// a stop right after the handler returned keeps the mark. A failure spends an attempt and sets
/// the moment before which the row is not claimed again, so the rows behind it go on at once,
/// and the last attempt makes the row dead. A rejection makes it dead on the spot. A stop of the
/// host during handling and a loop error (the claim, the mark or the connection failed) roll the
/// claim back and cost the row nothing.
/// </remarks>
public sealed class QueueWorker : BackgroundService
{
    /// <summary>How long an idle loop waits before it looks for a ready row again.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>The failure that makes a row dead, about 64 minutes after the first one.</summary>
    private const int MaxAttempts = 20;

    /// <summary>How much of the exception a row keeps as its last error.</summary>
    private const int LastErrorLength = 500;

    /// <summary>The longest pause of the retry ladder.</summary>
    private static readonly TimeSpan MaxRetryPause = TimeSpan.FromSeconds(300);

    /// <summary>The longest wait after loop errors in a row.</summary>
    private static readonly TimeSpan MaxLoopErrorPause = TimeSpan.FromSeconds(60);

    private const string RejectedError = "Rejected: the handler answered that the row cannot be processed";

    private readonly OutboxRowHandler _handler;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<QueueWorker> _logger;
    private readonly string _claimSql;
    private readonly int[]? _knownEventTypes;
    private readonly Func<TimeSpan, CancellationToken, Task> _wait;

    /// <param name="queue">The queue this loop drains, one of QueueNames.</param>
    /// <param name="knownEventTypes">The event types this build can decode, or null on a
    /// queue of letters. A row of any other type is never claimed and waits for a build
    /// that knows it.</param>
    /// <param name="dataSource">The process's one source of connections.</param>
    /// <param name="clock">The application's clock, read by the claim and the marks.</param>
    /// <param name="wait">How the loop waits between passes. A seam for tests, which drive the
    /// loop without spending its pauses in real time; the host leaves it out.</param>
    public QueueWorker(
        string queue,
        IEnumerable<int>? knownEventTypes,
        OutboxRowHandler handler,
        NpgsqlDataSource dataSource,
        IServiceScopeFactory scopeFactory,
        IDateTimeProvider clock,
        ILogger<QueueWorker> logger,
        Func<TimeSpan, CancellationToken, Task>? wait = null)
    {
        Queue = queue;
        _knownEventTypes = knownEventTypes?.ToArray();
        _handler = handler;
        _dataSource = dataSource;
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
        _wait = wait ?? Task.Delay;

        // Identifiers are constants; the queue, the moment and the types travel as parameters.
        _claimSql =
            $"""
             SELECT "Id", "EventId", "EventType", "EntityId", "Payload", "TraceParent", "Attempts"
             FROM "OutboxEvents"
             WHERE "Queue" = $1 AND "ProcessedUtc" IS NULL AND "DeadUtc" IS NULL
               AND ("NextAttemptUtc" IS NULL OR "NextAttemptUtc" <= $2)
               {(_knownEventTypes is null ? string.Empty : "AND \"EventType\" = ANY($3)")}
             ORDER BY "OccurredUtc", "Id"
             LIMIT 1
             FOR UPDATE SKIP LOCKED
             """;
    }

    /// <summary>The queue this loop drains.</summary>
    public string Queue { get; }

    /// <summary>Builds the loop of a queue from the services of the process.</summary>
    public static QueueWorker FromServices(
        IServiceProvider services, string queue, IEnumerable<int>? knownEventTypes, OutboxRowHandler handler) =>
        new(queue,
            knownEventTypes,
            handler,
            services.GetRequiredService<NpgsqlDataSource>(),
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IDateTimeProvider>(),
            services.GetRequiredService<ILogger<QueueWorker>>());

    /// <summary>
    /// Claims the oldest ready row, hands it to the handler and writes what came of it.
    /// </summary>
    /// <returns>Whether a row was claimed and its outcome written, so the caller keeps draining.</returns>
    /// <remarks>
    /// A fault of the claim or the mark is thrown: it is a loop error. A stop of the host during
    /// handling is thrown as a cancellation. Both leave the row as it was.
    /// </remarks>
    public async Task<bool> HandleNextAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var claim = await Claim(connection, transaction, cancellationToken);
        if (claim is null)
        {
            return false;
        }

        var (row, attempts) = claim.Value;
        var handling = await Handle(row, cancellationToken);

        // Without the stopping token: a stop right after the handler returned keeps the mark.
        var now = _clock.Now.ToUniversalTime();
        var queue = MessagingMetrics.Queue(Queue);
        Action count;
        if (handling.Error is { } error)
        {
            var dead = await MarkFailed(connection, transaction, row.Id, attempts + 1, error, now);
            count = dead
                ? () => MessagingMetrics.Failed.Add(1, queue, new("reason", error.GetType().Name))
                : () => MessagingMetrics.Retried.Add(1, queue);
        }
        else
        {
            var result = handling.Result;
            if (result == ProcessResult.Success)
            {
                await MarkProcessed(connection, transaction, row.Id, now);
            }
            else
            {
                await MarkDead(connection, transaction, row.Id, attempts, RejectedError, now);
            }

            count = () =>
            {
                MessagingMetrics.Consumed.Add(1, queue, new KeyValuePair<string, object?>("result", result.ToString()));
                if (result != ProcessResult.Success)
                {
                    MessagingMetrics.Failed.Add(1, queue, new("reason", "Rejected"));
                }
            };
        }

        await transaction.CommitAsync(CancellationToken.None);

        // Only a committed mark is counted: a lost commit is a loop error, and the row is
        // claimed again as if nothing had happened.
        count();
        return true;
    }

    /// <summary>The pause after the failure of the given number: two to its power in seconds, capped.</summary>
    /// <param name="attempt">Attempts spent, from one.</param>
    private static TimeSpan RetryPause(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(MaxRetryPause.TotalSeconds, Math.Pow(2, attempt)));

    /// <summary>
    /// What a row keeps of an exception: its type and message, without zero characters, which
    /// PostgreSQL text refuses, cut to <see cref="LastErrorLength"/> characters.
    /// </summary>
    public static string LastError(Exception exception)
    {
        var text = $"{exception.GetType().FullName}: {exception.Message}".Replace("\0", string.Empty);
        if (text.Length <= LastErrorLength)
        {
            return text;
        }

        // A cut through a surrogate pair would leave text the driver refuses to encode.
        var length = char.IsHighSurrogate(text[LastErrorLength - 1]) ? LastErrorLength - 1 : LastErrorLength;
        return text[..length];
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Everything before the first await runs inside host startup.
        await Task.Yield();

        var loopErrors = 0;
        try
        {
            // A pause after each drain rather than a timer: a long drain does not queue up ticks.
            while (true)
            {
                TimeSpan pause;
                try
                {
                    while (await HandleNextAsync(stoppingToken))
                    {
                    }

                    loopErrors = 0;
                    pause = PollInterval;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // A loop error: nothing leaves the loop, because by default that stops the host.
                    loopErrors++;
                    pause = LoopErrorPause(loopErrors);
                    _logger.LogWarning(exception,
                        "A pass over the queue {Queue} failed, the loop waits {Pause} before the next one",
                        Queue, pause);
                }

                await _wait(pause, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; an unfinished claim has rolled back.
        }
    }

    /// <summary>The wait after loop errors in a row: one second, doubling, capped.</summary>
    /// <param name="loopErrors">Loop errors in a row, from one.</param>
    private static TimeSpan LoopErrorPause(int loopErrors) =>
        TimeSpan.FromSeconds(Math.Min(MaxLoopErrorPause.TotalSeconds, Math.Pow(2, loopErrors - 1)));

    private async Task<(OutboxRow Row, int Attempts)?> Claim(
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(_claimSql, connection, transaction);
        command.Parameters.Add(new NpgsqlParameter { Value = Queue });
        command.Parameters.Add(new NpgsqlParameter { Value = _clock.Now.ToUniversalTime() });
        if (_knownEventTypes is not null)
        {
            command.Parameters.Add(new NpgsqlParameter
            {
                Value = _knownEventTypes,
                NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer,
            });
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var row = new OutboxRow(
            reader.GetInt64(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
        return (row, reader.GetInt32(6));
    }

    private async Task<Handling> Handle(OutboxRow row, CancellationToken cancellationToken)
    {
        // The span continues the trace of the request that stored the row, when it left one.
        ActivityContext.TryParse(row.TraceParent, null, out var parent);
        using var activity = DmActivitySource.Source.StartActivity(
            $"{Queue} process", ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.destination.name", Queue);

        var started = Stopwatch.GetTimestamp();
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await _handler(row, scope.ServiceProvider, cancellationToken);
            if (result != ProcessResult.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Rejected");
            }

            return new Handling(result, null);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            // The host is stopping: whatever the handler threw on its way out costs the row nothing.
            throw new OperationCanceledException(
                "The host stopped while a row was handled", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            // A failure, a cancellation from anything but the host's stop included.
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            _logger.LogWarning(exception, "Handling row {RowId} of the queue {Queue} failed", row.Id, Queue);
            return new Handling(ProcessResult.Failure, exception);
        }
        finally
        {
            MessagingMetrics.Duration.Record(
                Stopwatch.GetElapsedTime(started).TotalSeconds, MessagingMetrics.Queue(Queue));
        }
    }

    private static async Task MarkProcessed(
        NpgsqlConnection connection, NpgsqlTransaction transaction, long id, DateTimeOffset now)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE "OutboxEvents"
            SET "ProcessedUtc" = $1, "Payload" = NULL, "LastError" = NULL
            WHERE "Id" = $2
            """,
            connection, transaction);
        command.Parameters.Add(new NpgsqlParameter { Value = now });
        command.Parameters.Add(new NpgsqlParameter { Value = id });
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    /// <returns>Whether the row became dead: the last attempt of the ladder.</returns>
    private static async Task<bool> MarkFailed(
        NpgsqlConnection connection, NpgsqlTransaction transaction, long id, int attempts, Exception error,
        DateTimeOffset now)
    {
        if (attempts >= MaxAttempts)
        {
            await MarkDead(connection, transaction, id, attempts, LastError(error), now);
            return true;
        }

        await using var command = new NpgsqlCommand(
            """
            UPDATE "OutboxEvents"
            SET "Attempts" = $1, "LastError" = $2, "NextAttemptUtc" = $3
            WHERE "Id" = $4
            """,
            connection, transaction);
        command.Parameters.Add(new NpgsqlParameter { Value = attempts });
        command.Parameters.Add(new NpgsqlParameter { Value = LastError(error) });
        command.Parameters.Add(new NpgsqlParameter { Value = now + RetryPause(attempts) });
        command.Parameters.Add(new NpgsqlParameter { Value = id });
        await command.ExecuteNonQueryAsync(CancellationToken.None);
        return false;
    }

    private static async Task MarkDead(
        NpgsqlConnection connection, NpgsqlTransaction transaction, long id, int attempts, string lastError,
        DateTimeOffset now)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE "OutboxEvents"
            SET "Attempts" = $1, "LastError" = $2, "NextAttemptUtc" = NULL, "DeadUtc" = $3
            WHERE "Id" = $4
            """,
            connection, transaction);
        command.Parameters.Add(new NpgsqlParameter { Value = attempts });
        command.Parameters.Add(new NpgsqlParameter { Value = lastError });
        command.Parameters.Add(new NpgsqlParameter { Value = now });
        command.Parameters.Add(new NpgsqlParameter { Value = id });
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    /// <param name="Result">What the handler answered.</param>
    /// <param name="Error">What the handler threw: a failure, whatever the result says.</param>
    private readonly record struct Handling(ProcessResult Result, Exception? Error);
}
