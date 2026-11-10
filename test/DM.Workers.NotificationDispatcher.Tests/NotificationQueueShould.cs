using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// The notification worker claims events straight from the outbox table: a row goes in as the
/// producer stores it, and the proof is the call the handler received and the state of the row.
/// </summary>
/// <remarks>
/// Rows the generic loop drains go to a queue of their own per test, so a row one test leaves
/// behind is never claimed by another. The tests over the notification queue itself first mark
/// whatever is pending there as processed.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class NotificationQueueShould(NotificationDatabaseFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task HandAStoredEventToTheHandlerWithinFiveSecondsAndMarkIt()
    {
        await ProcessEverythingPendingOnTheNotificationQueue();
        var handler = new RecordingProcessor();
        await using var services = Services(handler);
        var worker = NotificationQueue.Worker(services);
        var row = await Store(QueueNames.Notifications, EventType.NewTopic);

        var watch = Stopwatch.StartNew();
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await NotificationDatabaseFixture.WaitUntil(async () => (await fixture.OutboxRow(row.Id)).ProcessedUtc is not null, Within);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        watch.Elapsed.Should().BeLessThan(Within, "the worker looks for ready rows once a second");
        handler.Calls.Should().ContainSingle().Which.Should().Be(
            (QueueNames.Notifications, EventType.NewTopic, row.EntityId!.Value, row.EventId),
            "the handler gets the stored event under the queue's name where the routing key was");
        (await fixture.OutboxRow(row.Id)).ProcessedUtc.Should().Be(Now, "the mark reads the application's clock");
    }

    [Fact]
    public async Task DrainEveryReadyRowOfTheQueue()
    {
        var queue = OwnQueue();
        var handled = new ConcurrentBag<long>();
        await using var services = Services(new RecordingProcessor());
        var rows = new[] { await Store(queue, EventType.NewTopic), await Store(queue, EventType.NewTopic) };
        var worker = Worker(services, queue, (row, _, _) =>
        {
            handled.Add(row.Id);
            return Task.FromResult(ProcessResult.Success);
        });

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await NotificationDatabaseFixture.WaitUntil(() => Task.FromResult(handled.Count == rows.Length), Within);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        handled.Should().BeEquivalentTo(rows.Select(row => row.Id));
    }

    /// <remarks>
    /// Mutations it must fail on: a claim without the row lock hands one row to both loops, and
    /// a lock that does not skip locked rows makes the second loop wait, so the barrier times out.
    /// </remarks>
    [Fact]
    public async Task HandleEachRowOnceWhenTwoLoopsClaimAtOnce()
    {
        var queue = OwnQueue();
        var rows = new[] { await Store(queue, EventType.NewTopic), await Store(queue, EventType.NewTopic) };
        var handled = new ConcurrentBag<long>();
        using var barrier = new Barrier(2);
        await using var services = Services(new RecordingProcessor());
        OutboxRowHandler waitForTheOtherLoop = (row, _, _) =>
        {
            handled.Add(row.Id);
            barrier.SignalAndWait(Within).Should().BeTrue("both loops hold a row at once");
            return Task.FromResult(ProcessResult.Success);
        };
        var first = Worker(services, queue, waitForTheOtherLoop);
        var second = Worker(services, queue, waitForTheOtherLoop);

        var claims = await Task.WhenAll(
            Task.Run(() => first.HandleNextAsync(CancellationToken.None)),
            Task.Run(() => second.HandleNextAsync(CancellationToken.None)));

        claims.Should().Equal(true, true);
        handled.Should().BeEquivalentTo(rows.Select(row => row.Id), "each row is handled exactly once");
        foreach (var row in rows)
        {
            (await fixture.OutboxRow(row.Id)).ProcessedUtc.Should().Be(Now);
        }
    }

    /// <remarks>Mutation it must fail on: a claim without the condition on the event type.</remarks>
    [Fact]
    public async Task LeaveARowOfAnUnknownTypeToALoopThatKnowsIt()
    {
        var queue = OwnQueue();
        const int typeOfANewerBuild = 9999;
        var row = await Store(queue, (EventType)typeOfANewerBuild);
        var handled = new ConcurrentBag<long>();
        await using var services = Services(new RecordingProcessor());
        OutboxRowHandler handler = (claimed, _, _) =>
        {
            handled.Add(claimed.Id);
            return Task.FromResult(ProcessResult.Success);
        };

        var oldBuild = Worker(services, queue, handler, NotificationQueue.KnownEventTypes());
        (await oldBuild.HandleNextAsync(CancellationToken.None)).Should().BeFalse(
            "a build that does not know the type cannot decode the event");
        handled.Should().BeEmpty();
        (await fixture.OutboxRow(row.Id)).ProcessedUtc.Should().BeNull("the row waits for a build that knows it");

        var newBuild = Worker(services, queue, handler,
            NotificationQueue.KnownEventTypes().Append(typeOfANewerBuild));
        (await newBuild.HandleNextAsync(CancellationToken.None)).Should().BeTrue();
        handled.Should().Equal(row.Id);
    }

    [Fact]
    public async Task ClaimAnEventNoGeneratorHandlesOnceAndMarkIt()
    {
        await ProcessEverythingPendingOnTheNotificationQueue();
        var generator = Substitute.For<INotificationGenerator>();
        generator.CanResolve(Arg.Any<EventType>()).Returns(false);
        var service = Substitute.For<INotificationService>();
        var handler = new CountingProcessor(new NotificationProcessor(
            [generator],
            service,
            Substitute.For<INotificationEmailSender>(),
            Substitute.For<INotificationBotSender>(),
            Substitute.For<IRealtimeNotificationProducer>(),
            NullLogger<NotificationProcessor>.Instance));
        await using var services = Services(handler);
        var worker = NotificationQueue.Worker(services);
        var row = await Store(QueueNames.Notifications, EventType.NewTopic);

        while (await worker.HandleNextAsync(CancellationToken.None))
        {
        }

        handler.Calls.Should().Be(1, "an event no generator handles costs one claim");
        await service.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
        (await fixture.OutboxRow(row.Id)).ProcessedUtc.Should().Be(Now, "and is marked processed");
    }

    [Fact]
    public async Task RunTheHandlerInAScopeOfItsOwnForEachRow()
    {
        var queue = OwnQueue();
        await Store(queue, EventType.NewTopic);
        await Store(queue, EventType.NewTopic);
        var scopes = new List<IServiceProvider>();
        await using var services = Services(new RecordingProcessor());
        var worker = Worker(services, queue, (_, scope, _) =>
        {
            scopes.Add(scope);
            return Task.FromResult(ProcessResult.Success);
        });

        while (await worker.HandleNextAsync(CancellationToken.None))
        {
        }

        scopes.Should().HaveCount(2).And.OnlyHaveUniqueItems("every row gets a scope of its own")
            .And.NotContain(services, "the root scope would keep a DbContext for the life of the process");
    }

    [Fact]
    public async Task ContinueTheTraceTheRowCarries()
    {
        var queue = OwnQueue();
        var stored = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);
        await Store(queue, EventType.NewTopic, $"00-{stored.TraceId}-{stored.SpanId}-01");
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DmActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        Activity? span = null;
        await using var services = Services(new RecordingProcessor());
        var worker = Worker(services, queue, (_, _, _) =>
        {
            span = Activity.Current;
            return Task.FromResult(ProcessResult.Success);
        });

        await worker.HandleNextAsync(CancellationToken.None);

        span.Should().NotBeNull("the worker opens a span for the row");
        span!.TraceId.Should().Be(stored.TraceId, "the span continues the trace of the request that stored the row");
        span.ParentSpanId.Should().Be(stored.SpanId);
    }

    [Fact]
    public async Task CountTheRowAndItsDurationUnderTheQueue()
    {
        var queue = OwnQueue();
        await Store(queue, EventType.NewTopic);
        var consumed = new ConcurrentBag<(string? Queue, string? Result)>();
        var durations = new ConcurrentBag<string?>();
        using var meters = new MeterListener();
        meters.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MessagingMetrics.MeterName &&
                instrument.Name is "dm.messaging.consumed" or "dm.messaging.duration")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meters.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            consumed.Add((Tag(tags, "queue"), Tag(tags, "result"))));
        meters.SetMeasurementEventCallback<double>((_, _, tags, _) => durations.Add(Tag(tags, "queue")));
        meters.Start();
        await using var services = Services(new RecordingProcessor());
        var worker = Worker(services, queue, (_, _, _) => Task.FromResult(ProcessResult.Success));

        await worker.HandleNextAsync(CancellationToken.None);

        consumed.Should().Contain((queue, nameof(ProcessResult.Success)));
        durations.Should().Contain(queue);
    }

    private static string? Tag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == name)
            {
                return tag.Value?.ToString();
            }
        }

        return null;
    }

    private static string OwnQueue() => $"test.{Guid.NewGuid():N}";

    private ServiceProvider Services(IProcessor<string, InvokedEvent> processor) => new ServiceCollection()
        .AddSingleton(NpgsqlDataSource.Create(fixture.ConnectionString))
        .AddSingleton<IDateTimeProvider>(new TestClock())
        .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
        .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
        .AddScoped(_ => processor)
        .BuildServiceProvider();

    private static QueueWorker Worker(
        IServiceProvider services, string queue, OutboxRowHandler handler, IEnumerable<int>? knownEventTypes = null) =>
        new(queue,
            knownEventTypes ?? NotificationQueue.KnownEventTypes(),
            handler,
            services.GetRequiredService<NpgsqlDataSource>(),
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IDateTimeProvider>(),
            NullLogger<QueueWorker>.Instance);

    private async Task<OutboxEvent> Store(string queue, EventType eventType, string? traceParent = null)
    {
        await using var context = fixture.CreateContext();
        var row = new OutboxEvent
        {
            EventId = Guid.NewGuid(),
            Queue = queue,
            EventType = eventType,
            EntityId = Guid.NewGuid(),
            TraceParent = traceParent,
            OccurredUtc = Now.AddMinutes(-1),
        };
        context.OutboxEvents.Add(row);
        await context.SaveChangesAsync();
        return row;
    }


    private async Task ProcessEverythingPendingOnTheNotificationQueue()
    {
        await using var context = fixture.CreateContext();
        await context.OutboxEvents
            .Where(row => row.Queue == QueueNames.Notifications && row.ProcessedUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.ProcessedUtc, Now.AddDays(-1)));
    }


    private sealed class TestClock : IDateTimeProvider
    {
        public DateTimeOffset Now => NotificationQueueShould.Now;
    }

    /// <summary>The event handler's contract, recording what it was called with.</summary>
    private sealed class RecordingProcessor : IProcessor<string, InvokedEvent>
    {
        public ConcurrentQueue<(string Key, EventType Type, Guid EntityId, Guid EventId)> Calls { get; } = new();

        public Task<ProcessResult> Process(string key, InvokedEvent message, CancellationToken cancellationToken)
        {
            Calls.Enqueue((key, message.Type, message.EntityId, message.EventId));
            return Task.FromResult(ProcessResult.Success);
        }
    }

    /// <summary>The unchanged handler, with the number of calls it got.</summary>
    private sealed class CountingProcessor(NotificationProcessor inner) : IProcessor<string, InvokedEvent>
    {
        private int _calls;

        public int Calls => _calls;

        public Task<ProcessResult> Process(string key, InvokedEvent message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return inner.Process(key, message, cancellationToken);
        }
    }
}
