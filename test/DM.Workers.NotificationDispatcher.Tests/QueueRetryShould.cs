using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Core.Tracing;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Infrastructure.Messaging.Queues;
using DM.Infrastructure.Persistence.Entities.Outbox;
using DM.Workers.NotificationDispatcher.Bot;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Email;
using DM.Workers.NotificationDispatcher.Notifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// The life of a row that does not succeed: a failure climbs the retry ladder by the test clock
/// while the rows behind it go on, a rejection is dead at once, and a stop of the host or a
/// fault of the loop costs the row nothing. A dead row comes back by the return command.
/// </summary>
/// <remarks>
/// Rows go into the table, the proof is the state of the row, the calls the handler got and the
/// counters of the queue. Each test drains a queue of its own, so rows of other tests never meet.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class QueueRetryShould(NotificationDatabaseFixture fixture)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(5);

    /// <remarks>
    /// Mutations it must fail on: marking the row before the handler runs, which calls it once,
    /// and waiting for the next attempt in memory, which holds the row behind it back.
    /// </remarks>
    [Fact]
    public async Task ClimbTheLadderTwentyTimesWithoutHoldingBackTheRowBehindAndThenDie()
    {
        var queue = OwnQueue();
        var clock = new MovingClock(Start);
        var failing = await Store(queue);
        var behind = await Store(queue, occurredLater: true);
        var callsOfTheFailingRow = 0;
        var behindHandled = false;
        using var counters = new QueueCounters(queue);
        await using var services = Services();
        var worker = Worker(services, queue, clock, (row, _, _) =>
        {
            if (row.Id == behind.Id)
            {
                behindHandled = true;
                return Task.FromResult(ProcessResult.Success);
            }

            callsOfTheFailingRow++;
            throw new TimeoutException("the mail server is down");
        });

        (await worker.HandleNextAsync(CancellationToken.None)).Should().BeTrue();
        callsOfTheFailingRow.Should().Be(1, "a failure ends the claim instead of retrying in memory");
        behindHandled.Should().BeFalse();
        (await worker.HandleNextAsync(CancellationToken.None)).Should().BeTrue();
        behindHandled.Should().BeTrue("a row in retry never holds back a ready row");
        (await fixture.OutboxRow(behind.Id)).ProcessedUtc.Should().Be(Start);

        var pauses = new List<double>();
        for (var pass = 0; pass < 50 && (await fixture.OutboxRow(failing.Id)).DeadUtc is null; pass++)
        {
            var next = (await fixture.OutboxRow(failing.Id)).NextAttemptUtc!.Value;
            pauses.Add((next - clock.Now).TotalSeconds);
            clock.Now = next - TimeSpan.FromMilliseconds(1);
            (await worker.HandleNextAsync(CancellationToken.None)).Should().BeFalse("the pause is not over yet");
            clock.Now = next;
            (await worker.HandleNextAsync(CancellationToken.None)).Should().BeTrue();
        }

        callsOfTheFailingRow.Should().Be(20);
        pauses.Should().Equal(
            [2, 4, 8, 16, 32, 64, 128, 256, 300, 300, 300, 300, 300, 300, 300, 300, 300, 300, 300],
            "two to the power of the attempt in seconds, capped at 300");
        var dead = await fixture.OutboxRow(failing.Id);
        dead.DeadUtc.Should().Be(clock.Now, "the twentieth failure makes the row dead by the application's clock");
        dead.Attempts.Should().Be(20);
        dead.LastError.Should().Be("System.TimeoutException: the mail server is down");
        counters.Retried.Should().Be(19, "each pause assigned is a retry");
        counters.Failed.Should().Be(1, "the row became dead once");

        clock.Now += TimeSpan.FromDays(1);
        (await worker.HandleNextAsync(CancellationToken.None)).Should().BeFalse("no worker claims a dead row");
    }

    /// <remarks>Mutation it must fail on: the whole exception kept as the last error, which PostgreSQL refuses.</remarks>
    [Fact]
    public async Task KeepTheExceptionCutAndWithoutZeroCharactersAsTheLastError()
    {
        var queue = OwnQueue();
        var row = await Store(queue);
        await using var services = Services();
        var worker = Worker(services, queue, new MovingClock(Start),
            (_, _, _) => throw new InvalidOperationException("\0" + new string('x', 600)));

        await worker.HandleNextAsync(CancellationToken.None);

        var failed = await fixture.OutboxRow(row.Id);
        failed.Attempts.Should().Be(1);
        failed.NextAttemptUtc.Should().Be(Start.AddSeconds(2));
        failed.LastError.Should().HaveLength(500).And.NotContain("\0")
            .And.StartWith("System.InvalidOperationException: x");
    }

    /// <remarks>Mutation it must fail on: a rejection left for the next pass like a failure.</remarks>
    [Fact]
    public async Task MakeARejectedRowDeadOnThatPassAndHandleTheRowBehindItInTheSamePass()
    {
        var queue = OwnQueue();
        var rejected = await Store(queue);
        var behind = await Store(queue, occurredLater: true);
        using var counters = new QueueCounters(queue);
        var passEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var services = Services();
        var worker = Worker(services, queue, new MovingClock(Start),
            (row, _, _) => Task.FromResult(row.Id == rejected.Id ? ProcessResult.Failure : ProcessResult.Success),
            (_, stoppingToken) =>
            {
                passEnded.TrySetResult();
                return Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            });

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await passEnded.Task.WaitAsync(Within);
            (await fixture.OutboxRow(rejected.Id)).DeadUtc.Should().Be(Start, "a rejection is dead at once");
            (await fixture.OutboxRow(rejected.Id)).Attempts.Should().Be(0, "a rejection spends no attempt");
            (await fixture.OutboxRow(behind.Id)).ProcessedUtc.Should().Be(Start, "the pass goes on past a dead row");
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        counters.Failed.Should().Be(1);
        counters.Retried.Should().Be(0);
    }

    /// <remarks>Mutation it must fail on: an attempt spent on a stop of the host.</remarks>
    [Fact]
    public async Task SpendNothingWhenTheHostStopsDuringHandling()
    {
        var queue = OwnQueue();
        var handled = await Store(queue);
        var waiting = await Store(queue, occurredLater: true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var services = Services();
        var worker = Worker(services, queue, new MovingClock(Start), async (_, _, stoppingToken) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return ProcessResult.Success;
        });

        await worker.StartAsync(CancellationToken.None);
        await entered.Task.WaitAsync(Within);
        await worker.StopAsync(CancellationToken.None);

        worker.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue("a stop ends the loop quietly");
        foreach (var row in new[] { await fixture.OutboxRow(handled.Id), await fixture.OutboxRow(waiting.Id) })
        {
            row.Attempts.Should().Be(0, "a deployment costs a row nothing");
            row.NextAttemptUtc.Should().BeNull();
            row.LastError.Should().BeNull();
            row.DeadUtc.Should().BeNull();
            row.ProcessedUtc.Should().BeNull("a row whose handling was cut is not marked handled");
        }
    }

    /// <remarks>Mutation it must fail on: the mark written with the stopping token.</remarks>
    [Fact]
    public async Task KeepTheMarkWhenTheHostStopsRightAfterASuccess()
    {
        var queue = OwnQueue();
        var row = await Store(queue);
        using var stopping = new CancellationTokenSource();
        await using var services = Services();
        var worker = Worker(services, queue, new MovingClock(Start), async (_, _, _) =>
        {
            await stopping.CancelAsync();
            return ProcessResult.Success;
        });

        (await worker.HandleNextAsync(stopping.Token)).Should().BeTrue();

        (await fixture.OutboxRow(row.Id)).ProcessedUtc.Should().Be(Start, "the work is done, and doing it again would duplicate it");
    }

    /// <remarks>Mutation it must fail on: every cancellation taken for the host's stop, which costs the row nothing.</remarks>
    [Fact]
    public async Task CountACancellationThatIsNotTheHostsStopAsAFailure()
    {
        var queue = OwnQueue();
        var row = await Store(queue);
        await using var services = Services();
        var worker = Worker(services, queue, new MovingClock(Start),
            (_, _, _) => throw new TaskCanceledException("the request to the bot timed out"));

        (await worker.HandleNextAsync(CancellationToken.None)).Should().BeTrue();

        var failed = await fixture.OutboxRow(row.Id);
        failed.Attempts.Should().Be(1, "only the host's stop is free");
        failed.NextAttemptUtc.Should().Be(Start.AddSeconds(2));
        failed.LastError.Should().StartWith("System.Threading.Tasks.TaskCanceledException");
    }

    /// <remarks>Mutation it must fail on: no catch around a pass, which ends the loop and stops the host.</remarks>
    [Fact]
    public async Task ClaimARowAgainForNothingWhenItsClaimSessionIsKilledAndDeliverOnce()
    {
        var queue = OwnQueue();
        var applicationName = $"claim-{Guid.NewGuid():N}";
        var row = await Store(queue);
        using var counters = new QueueCounters(queue);
        var email = Substitute.For<INotificationEmailSender>();
        var bot = Substitute.For<INotificationBotSender>();
        var calls = 0;
        await using var services = Services(email, bot, applicationName);
        var worker = Worker(services, queue, new MovingClock(Start), async (claimed, scope, stoppingToken) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                await KillSessionsOf(applicationName);
            }

            return await NotificationQueue.Handle(claimed, scope, stoppingToken);
        }, (_, stoppingToken) => Task.Delay(TimeSpan.FromMilliseconds(10), stoppingToken));

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await NotificationDatabaseFixture.WaitUntil(async () => (await fixture.OutboxRow(row.Id)).ProcessedUtc is not null, Within);
            worker.ExecuteTask!.IsCompleted.Should().BeFalse("a lost claim is a loop error, and the loop lives");
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        calls.Should().Be(2, "the mark was lost with the session, so the row is claimed again");
        var processed = await fixture.OutboxRow(row.Id);
        processed.Attempts.Should().Be(0, "a lost session is the loop's fault, not the row's");
        processed.LastError.Should().BeNull();
        counters.Failed.Should().Be(0);
        counters.Retried.Should().Be(0);

        await using var context = fixture.CreateContext();
        (await context.Notifications.CountAsync(n => n.EventId == row.EventId))
            .Should().Be(1, "the second handling is deduplicated by the notification's unique index");
        await email.Received(1).SendIfEnabled(
            Arg.Any<CreateNotification>(), Arg.Any<EventType>(), Arg.Any<CancellationToken>());
        await bot.Received(1).SendIfEnabled(
            Arg.Any<CreateNotification>(), Arg.Any<EventType>(), Arg.Any<CancellationToken>());
    }

    /// <remarks>
    /// A trigger deferred to the commit fails the commit of this queue alone, after the mark was
    /// written: the session dying between the mark and the commit. Mutation it must fail on: the
    /// counters added before the commit.
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CountNothingWhenTheCommitOfTheMarkFails(bool rejected)
    {
        var queue = OwnQueue();
        var row = await Store(queue);
        using var counters = new QueueCounters(queue);
        await using var services = Services();
        var worker = Worker(services, queue, new MovingClock(Start), (_, _, _) => rejected
            ? Task.FromResult(ProcessResult.Failure)
            : throw new TimeoutException("the mail server is down"));
        var trigger = $"fail_commit_{Guid.NewGuid():N}";
        await Run($"""
            CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN RAISE EXCEPTION 'the commit is lost'; END $$;
            CREATE CONSTRAINT TRIGGER {trigger} AFTER UPDATE ON "OutboxEvents"
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
            WHEN (NEW."Queue" = '{queue}') EXECUTE FUNCTION {trigger}();
            """);
        try
        {
            var handling = () => worker.HandleNextAsync(CancellationToken.None);
            await handling.Should().ThrowAsync<PostgresException>("a lost commit is a loop error");
        }
        finally
        {
            await Run($"""DROP TRIGGER {trigger} ON "OutboxEvents"; DROP FUNCTION {trigger}();""");
        }

        counters.Failed.Should().Be(0, "the row did not become dead, its mark rolled back");
        counters.Retried.Should().Be(0, "no pause was assigned, its mark rolled back");
        var untouched = await fixture.OutboxRow(row.Id);
        (untouched.Attempts, untouched.DeadUtc, untouched.NextAttemptUtc)
            .Should().Be((0, (DateTimeOffset?)null, (DateTimeOffset?)null));

        (await worker.HandleNextAsync(CancellationToken.None)).Should().BeTrue();
        (rejected ? counters.Failed : counters.Retried).Should().Be(1, "the mark that commits is counted once");
    }

    /// <remarks>Mutation it must fail on: a return command without the condition on DeadUtc.</remarks>
    [Fact]
    public async Task ReturnADeadRowByTheReturnCommandAndNothingElse()
    {
        var queue = OwnQueue();
        var dead = await Store(queue, row =>
        {
            row.Attempts = 20;
            row.LastError = "System.TimeoutException: the mail server is down";
            row.DeadUtc = Start.AddHours(-1);
        });
        var processed = await Store(queue, row =>
        {
            row.Attempts = 3;
            row.ProcessedUtc = Start.AddHours(-1);
        });
        var inRetry = await Store(queue, row =>
        {
            row.Attempts = 3;
            row.NextAttemptUtc = Start.AddMinutes(5);
        });

        (await Run(DeadRows.ReturnCommand(dead.Id))).Should().Be(1);
        (await Run(DeadRows.ReturnCommand(processed.Id))).Should().Be(0, "a processed row needs no return");
        (await Run(DeadRows.ReturnCommand(inRetry.Id))).Should().Be(0, "a row in retry is not dead");

        var returned = await fixture.OutboxRow(dead.Id);
        returned.DeadUtc.Should().BeNull();
        returned.Attempts.Should().Be(0, "a returned row gets the whole ladder again");
        returned.NextAttemptUtc.Should().BeNull();
        var untouched = await fixture.OutboxRow(processed.Id);
        (untouched.Attempts, untouched.ProcessedUtc, untouched.DeadUtc).Should().Be((3, Start.AddHours(-1), (DateTimeOffset?)null));
        var stillInRetry = await fixture.OutboxRow(inRetry.Id);
        (stillInRetry.Attempts, stillInRetry.NextAttemptUtc, stillInRetry.DeadUtc)
            .Should().Be((3, Start.AddMinutes(5), (DateTimeOffset?)null));

        var claimed = new List<long>();
        await using var services = Services();
        var worker = Worker(services, queue, new MovingClock(Start), (row, _, _) =>
        {
            claimed.Add(row.Id);
            return Task.FromResult(ProcessResult.Success);
        });
        (await worker.HandleNextAsync(CancellationToken.None)).Should().BeTrue();
        claimed.Should().Equal([dead.Id], "a returned row is ready at once");
    }

    private static string OwnQueue() => $"test.{Guid.NewGuid():N}";

    private ServiceProvider Services(
        INotificationEmailSender? email = null, INotificationBotSender? bot = null, string? applicationName = null)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            ApplicationName = applicationName ?? "queue-retry-tests",
        }.ConnectionString;
        return new ServiceCollection()
            .AddSingleton(NpgsqlDataSource.Create(connectionString))
            .AddScoped<IProcessor<string, InvokedEvent>>(_ =>
            {
                var context = fixture.CreateContext();
                var generator = Substitute.For<INotificationGenerator>();
                generator.CanResolve(Arg.Any<EventType>()).Returns(true);
                generator.Generate(Arg.Any<Guid>()).Returns(_ => NotificationSeed.One(new CreateNotification
                {
                    UsersInterested = [NotificationSeed.MasterId],
                    Metadata = new { Marker = "queue-retry" },
                }));
                return new NotificationProcessor(
                    [generator],
                    EventReplayShould.RealNotificationService(context),
                    email ?? Substitute.For<INotificationEmailSender>(),
                    bot ?? Substitute.For<INotificationBotSender>(),
                    Substitute.For<IRealtimeNotificationProducer>(),
                    NullLogger<NotificationProcessor>.Instance);
            })
            .BuildServiceProvider();
    }

    private static QueueWorker Worker(
        IServiceProvider services, string queue, IDateTimeProvider clock, OutboxRowHandler handler,
        Func<TimeSpan, CancellationToken, Task>? wait = null) =>
        new(queue,
            NotificationQueue.KnownEventTypes(),
            handler,
            services.GetRequiredService<NpgsqlDataSource>(),
            services.GetRequiredService<IServiceScopeFactory>(),
            clock,
            NullLogger<QueueWorker>.Instance,
            wait);

    private Task<OutboxEvent> Store(string queue, bool occurredLater = false) =>
        Store(queue, row => row.OccurredUtc += occurredLater ? TimeSpan.FromSeconds(1) : TimeSpan.Zero);

    private async Task<OutboxEvent> Store(string queue, Action<OutboxEvent> shape)
    {
        await using var context = fixture.CreateContext();
        var row = new OutboxEvent
        {
            EventId = Guid.NewGuid(),
            Queue = queue,
            EventType = EventType.NewTopic,
            EntityId = Guid.NewGuid(),
            OccurredUtc = Start.AddMinutes(-1),
        };
        shape(row);
        context.OutboxEvents.Add(row);
        await context.SaveChangesAsync();
        return row;
    }


    private async Task<int> Run(string command)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var sql = new NpgsqlCommand(command, connection);
        return await sql.ExecuteNonQueryAsync();
    }

    /// <summary>What an operator or a failover does: ends the sessions of the claim from the database.</summary>
    private async Task KillSessionsOf(string applicationName)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var kill = new NpgsqlCommand(
            "SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity WHERE application_name = $1",
            connection);
        kill.Parameters.Add(new NpgsqlParameter { Value = applicationName });
        ((long)(await kill.ExecuteScalarAsync())!).Should().BeGreaterThan(0, "the claim's session is open");
    }


    private sealed class MovingClock(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
    }

    /// <summary>The failed and retried counters of one queue.</summary>
    private sealed class QueueCounters : IDisposable
    {
        private readonly MeterListener _listener = new();
        private int _failed;
        private int _retried;

        public QueueCounters(string queue)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MessagingMetrics.MeterName &&
                    instrument.Name is "dm.messaging.failed" or "dm.messaging.retried")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "queue" && Equals(tag.Value, queue))
                    {
                        if (instrument.Name == "dm.messaging.failed")
                        {
                            Interlocked.Add(ref _failed, (int)value);
                        }
                        else
                        {
                            Interlocked.Add(ref _retried, (int)value);
                        }
                    }
                }
            });
            _listener.Start();
        }

        public int Failed => _failed;

        public int Retried => _retried;

        public void Dispose() => _listener.Dispose();
    }
}
