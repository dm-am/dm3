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
    /// Wait before the attempt of the given number. Injected so a test can drive
    /// the loop without spending the ladder in real time; the host leaves it out.
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

        // Forever, where both workers stop after five attempts. The difference is
        // what giving up costs there and here: a worker lets the exception stop its
        // host and the container policy starts it again, so its ladder ends in
        // another ladder. This host has a site to serve, so the same exception is
        // swallowed below and nothing tries again. Five attempts, 62 seconds end to
        // end, are shorter than a broker restart, which is how an API that came up
        // first ended up dropping every push for the life of the process.
        //
        // One warning per attempt: at the cap, two log lines a minute for as long
        // as the broker is unreachable. Cheaper than what it replaces, where the
        // last of five warnings was the only difference between a consumer that
        // gave up and one nobody was pushing to.
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
    /// shared/api/client.ts), deliberately the same on both ends of the push path:
    /// the cap is what keeps an outage of any length from being followed by
    /// minutes of silence after the broker is back.
    /// </remarks>
    /// <param name="attempt">Number of the attempt about to be waited for, from one.</param>
    private static TimeSpan DefaultRetryDelay(int attempt) => TimeSpan.FromMilliseconds(
        Math.Min(MaxRetryDelay.TotalMilliseconds, 1000d * Math.Pow(2, attempt - 1)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[🚴] Starting realtime notifications consumer");

        // Yield before touching the broker: work done synchronously here runs as
        // part of host startup, so a RabbitMQ outage would abort the whole host.
        await Task.Yield();

        var parameters = new DmConsumerParameters("dm.api", QueueName)
        {
            ExchangeName = RealtimeNotificationsTransport.ExchangeName,
            RoutingKeys = ["#"],

            // No dead-letter exchange here, unlike the queues the workers consume.
            // This message is a copy of a notification the dispatcher has already
            // stored, and the client reads that over REST — a push kept past its
            // moment gives nobody anything to act on. Dropping it is the decision,
            // not an omission.
            //
            // Exclusive also names the queue with a random suffix on every
            // subscription, which is why the alert about this queue matches it
            // by prefix.
            Exclusive = true
        };

        try
        {
            var consumer = _consumerBuilder.Build<RealtimeNotification, RealtimeNotificationProcessor>(parameters);

            // Retried under the token the host stops with, the way both workers do
            // it. Without the token a stop arriving inside a broker outage waits
            // the current attempt out with nothing able to interrupt it, and with
            // the loop below that wait no longer even has a last attempt to end at.
            await _consumeRetryPolicy.ExecuteAsync(async token =>
            {
                await consumer.Subscribe(token);
            }, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping between attempts, which is not a failure worth
            // reporting: the message below would name a broker outage that is not
            // happening.
            return;
        }
        catch (Exception exception)
        {
            // Realtime push is an enhancement, and the site keeps serving without
            // it. Letting this escape would stop the host
            // (BackgroundServiceExceptionBehavior.StopHost is the default), so one
            // failure here would take the entire API down with it — and, under a
            // container restart policy, into a crash loop.
            //
            // A broker that is merely unreachable does not arrive here at all: the
            // policy above handles every exception of the subscription and keeps
            // going. What is left for this branch is the composition around it,
            // the build of the consumer above chief among them, and that does not
            // become right by being tried again. Push stays off for the life of the
            // process, and the health check is what says so out loud, because the
            // subscription is never marked below.
            _logger.LogError(exception,
                "[💥] Realtime notifications consumer could not be started for {QueueName}; " +
                "realtime push is unavailable on this instance",
                parameters.QueueName);
            return;
        }

        // After the policy returns, which it does only on a subscription that
        // opened: from here the health check answers Healthy for this instance.
        _subscriptionState.MarkSubscribed();

        _logger.LogDebug("[👂] Realtime notifications consumer is listening to {QueueName} queue",
            parameters.QueueName);
    }
}
