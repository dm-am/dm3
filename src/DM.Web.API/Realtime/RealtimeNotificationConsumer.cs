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

    private readonly ILogger<RealtimeNotificationConsumer> _logger;
    private readonly IDmConsumerBuilder _consumerBuilder;
    private readonly AsyncRetryPolicy _consumeRetryPolicy;

    public RealtimeNotificationConsumer(
        ILogger<RealtimeNotificationConsumer> logger,
        IDmConsumerBuilder consumerBuilder)
    {
        _logger = logger;
        _consumerBuilder = consumerBuilder;
        _consumeRetryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => _logger.LogWarning(exception, "Could not subscribe to the queue"));
    }

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
            // it. The waits double from one second over five attempts, 62 seconds
            // end to end: the synchronous overload spent them in Thread.Sleep on a
            // pool thread and was handed no token at all, so a stop arriving inside
            // a broker outage waited every remaining attempt out with nothing able
            // to interrupt it.
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
            // (BackgroundServiceExceptionBehavior.StopHost is the default), so a
            // broker outage would take the entire API down with it — and, under
            // a container restart policy, into a crash loop. Push stays dead
            // until the next restart.
            //
            // What the client does without it, exactly: the global chat is the one
            // surface with a polling fallback of its own. The two badges have
            // none — they are re-read when the application mounts and on every
            // transition of the socket into the connected state (App.vue), so
            // with the consumer dead they freeze for the length of the SPA
            // session rather than forever. Nothing else falls back to REST at
            // all; saying "the client falls back to REST polling" described one
            // page as if it were the whole site.
            _logger.LogError(exception,
                "[💥] Realtime notifications consumer failed to subscribe to {QueueName}; " +
                "realtime push is unavailable until the API restarts",
                parameters.QueueName);
            return;
        }

        _logger.LogDebug("[👂] Realtime notifications consumer is listening to {QueueName} queue",
            parameters.QueueName);
    }
}
