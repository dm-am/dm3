using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DM.Web.API.Realtime;

internal class RealtimeNotificationConsumer : BackgroundService
{
    /// <summary>Queue this host reads, as both the topology and the metrics name it.</summary>
    internal const string QueueName = "dm.notifications.api";

    /// <summary>Longest wait between two subscription attempts.</summary>
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly ILogger<RealtimeNotificationConsumer> _logger;
    private readonly IDmConsumerBuilder _consumerBuilder;
    private readonly RealtimeSubscriptionState _subscriptionState;
    private readonly AsyncRetryPolicy _consumeRetryPolicy;

    /// <param name="logger">Logger.</param>
    /// <param name="consumerBuilder">Builder of the subscription.</param>
    /// <param name="subscriptionState">Holder the health check reads.</param>
    /// <param name="retryDelay">
    /// Wait before the attempt of the given number. A seam for tests, which drive the
    /// loop without spending the ladder in real time; the host leaves it out.
    /// </param>
    public RealtimeNotificationConsumer(
        ILogger<RealtimeNotificationConsumer> logger,
        IDmConsumerBuilder consumerBuilder,
        RealtimeSubscriptionState subscriptionState,
        Func<int, TimeSpan>? retryDelay = null)
    {
        _logger = logger;
        _consumerBuilder = consumerBuilder;
        _subscriptionState = subscriptionState;

        // Forever, unlike the workers: their host restarts after giving up, this one
        // has a site to serve and would drop every push for the life of the process.
        _consumeRetryPolicy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
            retryDelay ?? DefaultRetryDelay,
            (exception, attempt, wait) => _logger.LogWarning(exception,
                "Could not subscribe to the queue on attempt {Attempt}, retrying in {Wait}",
                attempt, wait));
    }

    /// <summary>
    /// Wait before the attempt of the given number: one second, doubling, capped.
    /// </summary>
    /// <remarks>
    /// The ladder the browser reconnects its socket on (hubRetryPolicy in
    /// shared/api/client.ts): the cap keeps an outage from being followed by
    /// minutes of silence after the broker is back.
    /// </remarks>
    /// <param name="attempt">Number of the attempt about to be waited for, from one.</param>
    private static TimeSpan DefaultRetryDelay(int attempt) => TimeSpan.FromMilliseconds(
        Math.Min(MaxRetryDelay.TotalMilliseconds, 1000d * Math.Pow(2, attempt - 1)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Work done before the first await runs inside host startup.
        await Task.Yield();

        var parameters = new DmConsumerParameters("dm.api", QueueName)
        {
            ExchangeName = RealtimeNotificationsTransport.ExchangeName,
            RoutingKeys = ["#"],

            // No dead-letter exchange: the push copies a notification the client
            // reads over REST, and one kept past its moment helps nobody. Exclusive
            // adds a random suffix to the name, so the alert matches it by prefix.
            Exclusive = true
        };

        try
        {
            var consumer = _consumerBuilder.Build<RealtimeNotification, RealtimeNotificationProcessor>(parameters);

            // Under the stopping token, so a stop inside a broker outage interrupts the wait.
            await _consumeRetryPolicy.ExecuteAsync(async token =>
            {
                await consumer.Subscribe(token);
            }, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            // An unreachable broker never arrives here, the policy retries it. What
            // does is the composition, and letting it escape would stop the host;
            // the health check reports push as off, since nothing marks it below.
            _logger.LogError(exception,
                "[💥] Realtime notifications consumer could not be started for {QueueName}; " +
                "realtime push is unavailable on this instance",
                parameters.QueueName);
            return;
        }

        _subscriptionState.MarkSubscribed();
    }
}
