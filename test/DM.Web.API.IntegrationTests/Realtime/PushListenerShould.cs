using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Persistence.Pushes;
using DM.Testing;
using DM.Web.API.Features.Personal.Notifications;
using DM.Web.API.Notifications;
using DM.Web.API.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using NSubstitute;
using Xunit;

namespace DM.Web.API.IntegrationTests.Realtime;

/// <summary>
/// The listener of an API instance over the real database: what it hears reaches the hub,
/// its health tells the present, and nothing it meets stops the host.
/// </summary>
[Collection("Database")]
public class PushListenerShould(DatabaseFixture fixture) : IAsyncDisposable
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(5);

    private readonly PushListenerState _state = new();
    private readonly ConcurrentQueue<Notification> _sentToEveryTab = new();
    private readonly CancellationTokenSource _stopping = new();
    private PushListener? _listener;

    // Mutation it must fail on: the state is not cleared when the connection is lost.
    [Fact]
    public async Task TellThePresentAcrossALostConnectionAndHearTheNextPush()
    {
        var reconnecting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var proceed = new SemaphoreSlim(0);
        var gauge = new ConcurrentQueue<long>();
        using var gaugeListener = ListenToGauge(gauge);

        await Start(_ =>
        {
            // The ladder is waited after the state was cleared and before the next attempt.
            reconnecting.TrySetResult();
            proceed.Wait(Within);
            return TimeSpan.Zero;
        });
        await Eventually(HealthStatus.Healthy);
        gauge.Should().Contain(1, "the gauge says the instance listens");

        await TerminateTheListenersConnection();
        await reconnecting.Task.WaitAsync(Within);

        (await Report()).Should().Be(HealthStatus.Degraded, "the connection the listener held is gone");
        gauge.Last().Should().Be(0, "the alert reads the gauge, and the gauge reads the state");

        proceed.Release();
        await Eventually(HealthStatus.Healthy);

        var push = Broadcast();
        await PushSender.SendAsync(DataSource, push, CancellationToken.None);

        await Eventually(() => _sentToEveryTab.Any(n => n.Id == push.NotificationId),
            "the next push after a reconnect reaches the tabs");
        _listener!.ExecuteTask!.IsCompleted.Should().BeFalse("the process lives through a lost connection");
    }

    [Fact]
    public async Task NeverHandAForeignShapeToTheHubAndCountIt()
    {
        // Written once, by the connection's reading, and read after the push behind it arrived.
        var channels = new List<string?>();
        using var counter = CounterTags.Listen("dm.messaging.delivery_failed", "channel", channels);
        await Start(_ => TimeSpan.Zero);
        await Eventually(HealthStatus.Healthy);

        await using (var forger = DataSource.CreateCommand("SELECT pg_notify($1, $2)"))
        {
            forger.Parameters.Add(new NpgsqlParameter { Value = QueueNames.PushChannel });
            forger.Parameters.Add(new NpgsqlParameter { Value = """{"script":"alert(1)"}""" });
            await forger.ExecuteNonQueryAsync();
        }

        // A push of the site's own shape after it: once it arrives, the forged one was read too.
        var push = Broadcast();
        await PushSender.SendAsync(DataSource, push, CancellationToken.None);
        await Eventually(() => _sentToEveryTab.Any(n => n.Id == push.NotificationId), "the site's push arrives");

        _sentToEveryTab.Should().ContainSingle("the forged notification never reaches an open tab");
        channels.Should().Contain("realtime", "a dropped notification is counted under the realtime channel");
    }

    private NpgsqlDataSource? _dataSource;

    private NpgsqlDataSource DataSource => _dataSource ??= NpgsqlDataSource.Create(fixture.ConnectionString);

    private async Task Start(Func<int, TimeSpan> ladder)
    {
        var hub = Substitute.For<IHubContext<NotificationHub, INotificationHub>>();
        var everyTab = Substitute.For<INotificationHub>();
        everyTab.Send(Arg.Do<Notification>(_sentToEveryTab.Enqueue)).Returns(Task.CompletedTask);
        hub.Clients.All.Returns(everyTab);

        var services = new ServiceCollection()
            .AddSingleton(hub)
            .AddSingleton(Substitute.For<IUserConnectionService>())
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();

        _listener = new PushListener(
            NullLogger<PushListener>.Instance,
            Options.Create(new ConnectionStrings { Rdb = fixture.ConnectionString }),
            _state,
            services.GetRequiredService<IServiceScopeFactory>(),
            ladder);
        await _listener.StartAsync(_stopping.Token);
    }

    private static RealtimeNotification Broadcast() => new()
    {
        NotificationId = Guid.NewGuid(),
        EventType = EventType.NewGlobalChatMessage,
        CreatedUtc = DateTimeOffset.UtcNow,
        RecipientIds = [],
        Metadata = new { text = "hello" }
    };

    private async Task TerminateTheListenersConnection()
    {
        await using var terminate = DataSource.CreateCommand(
            "SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity WHERE application_name = $1");
        terminate.Parameters.Add(new NpgsqlParameter { Value = PushListener.ApplicationName });
        ((long)(await terminate.ExecuteScalarAsync())!).Should().BeGreaterThan(0,
            "the listener holds a connection of its own, named so it can be found");
    }

    private async Task<HealthStatus> Report() =>
        (await _state.CheckHealthAsync(new HealthCheckContext())).Status;

    private async Task Eventually(HealthStatus expected) =>
        await Eventually(() => Report().GetAwaiter().GetResult() == expected, $"the push check reads {expected}");

    private static async Task Eventually(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow + Within;
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, because);
            await Task.Delay(20);
        }
    }

    private static MeterListener ListenToGauge(ConcurrentQueue<long> values)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, self) =>
            {
                if (instrument.Name == "dm.messaging.push_listening")
                {
                    self.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => values.Enqueue(value));
        listener.Start();
        return listener;
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        if (_listener?.ExecuteTask is { } running)
        {
            await running.WaitAsync(Within);
        }

        _listener?.Dispose();
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        _stopping.Dispose();
        GC.SuppressFinalize(this);
    }
}
