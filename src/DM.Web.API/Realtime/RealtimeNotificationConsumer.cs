using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Rabbit.Consuming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DM.Web.API.Realtime;

internal class RealtimeNotificationConsumer : BackgroundService
{
    private readonly ILogger<RealtimeNotificationConsumer> _logger;
    private readonly IConsumerBuilder _consumerBuilder;
    private readonly RetryPolicy _consumeRetryPolicy;

    public RealtimeNotificationConsumer(
        ILogger<RealtimeNotificationConsumer> logger,
        IConsumerBuilder consumerBuilder)
    {
        _logger = logger;
        _consumerBuilder = consumerBuilder;
        _consumeRetryPolicy = Policy.Handle<Exception>().WaitAndRetry(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => _logger.LogWarning(exception, "Could not subscribe to the queue"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[🚴] Starting realtime notifications consumer");

        // Yield before touching the broker: work done synchronously here runs as
        // part of host startup, so a RabbitMQ outage would abort the whole host.
        await Task.Yield();

        var parameters = new RabbitConsumerParameters("dm.api", "dm.notifications.api", ProcessingOrder.Sequential)
        {
            ExchangeName = "dm.notifications.sent",
            RoutingKeys = new[] { "#" },
            Exclusive = true
        };

        try
        {
            var consumer = _consumerBuilder.BuildRabbit<RealtimeNotification, RealtimeNotificationProcessor>(parameters);
            _consumeRetryPolicy.Execute(consumer.Subscribe);
        }
        catch (Exception exception)
        {
            // Realtime push is an enhancement: the client falls back to REST
            // polling. Letting this escape would stop the host
            // (BackgroundServiceExceptionBehavior.StopHost is the default), so a
            // broker outage would take the entire API down with it — and, under
            // a container restart policy, into a crash loop. Push stays dead
            // until the next restart; the site keeps serving.
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