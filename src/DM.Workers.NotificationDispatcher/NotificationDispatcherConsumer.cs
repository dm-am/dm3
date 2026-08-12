using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Notifiers;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Rabbit.Consuming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;

namespace DM.Workers.NotificationDispatcher;

internal class NotificationDispatcherConsumer : BackgroundService
{
    /// <summary>Queue this worker reads, as both the topology and the metrics name it.</summary>
    internal const string QueueName = "dm.notifications";

    private const string DeadLetterExchangeName = "dm.notifications.undelivered";

    private readonly ILogger<NotificationDispatcherConsumer> _logger;
    private readonly IConsumerBuilder _consumerBuilder;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAsyncConnectionFactory _rabbitConnectionFactory;
    private readonly AsyncRetryPolicy _consumeRetryPolicy;

    public NotificationDispatcherConsumer(
        ILogger<NotificationDispatcherConsumer> logger,
        IConsumerBuilder consumerBuilder,
        IServiceProvider serviceProvider,
        IAsyncConnectionFactory rabbitConnectionFactory)
    {
        _logger = logger;
        _consumerBuilder = consumerBuilder;
        _serviceProvider = serviceProvider;
        _rabbitConnectionFactory = rabbitConnectionFactory;
        _consumeRetryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => _logger.LogWarning(exception, "Could not subscribe to the queue"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[🚴] Starting notifications consumer");

        // Yield before touching the broker: everything before the first await runs
        // inside host startup, so a broker that is not up yet aborted the host before
        // its own health check could report why, and the retry below held the start
        // for a minute of Thread.Sleep first. The API consumer next door has done it
        // this way all along.
        await Task.Yield();

        var parameters = new RabbitConsumerParameters("dm.notifications", QueueName, ProcessingOrder.Unmanaged)
        {
            ExchangeName = InvokedEventsTransport.ExchangeName,
            RoutingKeys = ResolveHandledEventTypes().ToRoutingKeys(),

            // Without this the queue has no dead-letter exchange, so an event a
            // generator keeps throwing on is rejected into nothing: the retries run
            // out, the exception escapes the middleware, and the broker drops the
            // message. The recipient is never told, and all that is left of the
            // event is five warnings in the log.
            DeadLetterExchange = DeadLetterExchangeName,
        };
        var consumer = _consumerBuilder.BuildRabbit<InvokedEvent, NotificationProcessor>(parameters);

        // The dead-letter declaration is inside the policy with the subscription: it
        // opens its own connection to the same broker, and it used to be the one call
        // nothing retried, so an unreachable broker threw past Polly entirely.
        await _consumeRetryPolicy.ExecuteAsync(_ =>
        {
            DeadLetterQueue.DeclareTerminal(_rabbitConnectionFactory, DeadLetterExchangeName);
            consumer.Subscribe();
            return Task.CompletedTask;
        }, stoppingToken);

        _logger.LogDebug("[👂] Notifications consumer is listening to {QueueName} queue", parameters.QueueName);
    }

    /// <summary>
    /// The queue binds exactly the events some generator declares it can handle.
    /// Derived rather than listed: a hand-maintained mirror of the generators is
    /// what silently starved 14 of them — the event was published, no binding
    /// matched, and RabbitMQ dropped it without a trace.
    /// Instances are resolved in a scope and released immediately; holding them
    /// on this singleton would capture their scoped dependencies.
    /// </summary>
    private EventType[] ResolveHandledEventTypes()
    {
        using var scope = _serviceProvider.CreateScope();
        var generators = scope.ServiceProvider.GetRequiredService<IEnumerable<INotificationGenerator>>().ToArray();
        return Enum.GetValues<EventType>()
            .Where(eventType => generators.Any(generator => generator.CanResolve(eventType)))
            .ToArray();
    }
}
