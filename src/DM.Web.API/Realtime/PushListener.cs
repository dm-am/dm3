using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Core.Tracing;
using DM.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DM.Web.API.Realtime;

/// <summary>
/// Listens on the push channel of the database and hands every push it hears to the
/// push handler, which sends it to the open tabs over the hub.
/// </summary>
/// <remarks>
/// A connection of its own, outside the pool and with keepalive, since a pooled one would
/// be returned and lose its LISTEN, and a silent network would hold a dead one forever.
/// A push sent while the listener reconnects is lost; the tab is right again after a page
/// load or a socket reconnect.
/// </remarks>
internal sealed class PushListener : BackgroundService
{
    /// <summary>Pushes heard and not yet handed to the hub; one heard past it is dropped and counted.</summary>
    internal const int BufferCapacity = 1000;

    /// <summary>How the listener's connection names itself in pg_stat_activity.</summary>
    internal const string ApplicationName = "dm-push-listener";

    /// <summary>The channel the delivery failures of pushes are counted under.</summary>
    private const string RealtimeChannel = "realtime";

    /// <summary>Longest wait between two connection attempts.</summary>
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>Seconds between keepalive queries on the idle connection.</summary>
    private const int KeepAliveSeconds = 30;

    private readonly ILogger<PushListener> _logger;
    private readonly IOptions<ConnectionStrings> _connectionStrings;
    private readonly PushListenerState _state;
    private readonly IServiceScopeFactory _scopes;
    private readonly Func<int, TimeSpan> _retryDelay;

    private readonly Channel<RealtimeNotification> _buffer = Channel.CreateBounded<RealtimeNotification>(
        new BoundedChannelOptions(BufferCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    /// <param name="connectionStrings">Where the database is.</param>
    /// <param name="state">Holder the health check and the gauge read.</param>
    /// <param name="scopes">Scopes the push handler is resolved in, one per push.</param>
    /// <param name="retryDelay">
    /// Wait before the attempt of the given number. A seam for tests, which drive the
    /// loop without spending the ladder in real time; the host leaves it out.
    /// </param>
    public PushListener(
        ILogger<PushListener> logger,
        IOptions<ConnectionStrings> connectionStrings,
        PushListenerState state,
        IServiceScopeFactory scopes,
        Func<int, TimeSpan>? retryDelay = null)
    {
        _logger = logger;
        _connectionStrings = connectionStrings;
        _state = state;
        _scopes = scopes;
        _retryDelay = retryDelay ?? DefaultRetryDelay;
    }

    /// <summary>
    /// Wait before the attempt of the given number: one second, doubling, capped.
    /// </summary>
    /// <remarks>
    /// The ladder the browser reconnects its socket on (hubRetryPolicy in
    /// shared/api/client.ts): the cap keeps an outage from being followed by
    /// minutes of silence after the database is back.
    /// </remarks>
    /// <param name="attempt">Number of the attempt about to be waited for, from one.</param>
    private static TimeSpan DefaultRetryDelay(int attempt) => TimeSpan.FromMilliseconds(
        Math.Min(MaxRetryDelay.TotalMilliseconds, 1000d * Math.Pow(2, attempt - 1)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Work done before the first await runs inside host startup.
        await Task.Yield();

        var delivering = DeliverAsync(stoppingToken);
        await ListenAsync(stoppingToken);
        _buffer.Writer.TryComplete();
        await delivering;
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(ListenerConnectionString());
                connection.Notification += OnNotification;
                await connection.OpenAsync(stoppingToken);
                await using (var listen = new NpgsqlCommand($"LISTEN {QueueNames.PushChannel}", connection))
                {
                    await listen.ExecuteNonQueryAsync(stoppingToken);
                }

                _state.MarkListening();
                attempt = 0;
                _logger.LogInformation("Listening for pushes on {Channel}", QueueNames.PushChannel);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Whatever the connection threw, the listener reconnects: letting it escape
                // would stop the host, and a host without push still serves every request.
                _logger.LogWarning(exception,
                    "Push listener lost its connection on attempt {Attempt}", attempt + 1);
            }
            finally
            {
                _state.MarkNotListening();
            }

            attempt++;
            try
            {
                await Task.Delay(_retryDelay(attempt), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private string ListenerConnectionString() =>
        new NpgsqlConnectionStringBuilder(_connectionStrings.Value.Rdb)
        {
            Pooling = false,
            KeepAlive = KeepAliveSeconds,
            Enlist = false,
            ApplicationName = ApplicationName
        }.ConnectionString;

    private void OnNotification(object sender, NpgsqlNotificationEventArgs notification) => Hear(notification.Payload);

    /// <summary>Takes one heard notification: checks its shape and buffers the push.</summary>
    /// <remarks>Runs on the connection's reading, so it does nothing more.</remarks>
    internal void Hear(string payload)
    {
        if (!PushPayload.TryRead(payload, out var push))
        {
            CountFailure(null);
            _logger.LogWarning("Dropped a notification of a foreign shape on {Channel}", QueueNames.PushChannel);
            return;
        }

        if (!_buffer.Writer.TryWrite(push))
        {
            CountFailure(push);
            _logger.LogWarning("Dropped push {NotificationId}: the buffer of {Capacity} is full",
                push.NotificationId, BufferCapacity);
        }
    }

    private async Task DeliverAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var push in _buffer.Reader.ReadAllAsync(stoppingToken))
            {
                await DeliverOneAsync(push, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task DeliverOneAsync(RealtimeNotification push, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<RealtimeNotificationProcessor>();
            if (await handler.Process(QueueNames.PushChannel, push, stoppingToken) != ProcessResult.Success)
            {
                CountFailure(push);
                _logger.LogWarning("The hub refused push {NotificationId}", push.NotificationId);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // One push the hub refused must not end the delivery of the ones behind it.
            CountFailure(push);
            _logger.LogWarning(exception, "Failed to hand push {NotificationId} to the hub", push.NotificationId);
        }
    }

    private static void CountFailure(RealtimeNotification? push)
    {
        if (push is null)
        {
            MessagingMetrics.DeliveryFailed.Add(1, MessagingMetrics.Channel(RealtimeChannel));
            return;
        }

        MessagingMetrics.DeliveryFailed.Add(1,
            MessagingMetrics.Channel(RealtimeChannel),
            new KeyValuePair<string, object?>("event", push.EventType.ToString()));
    }
}
