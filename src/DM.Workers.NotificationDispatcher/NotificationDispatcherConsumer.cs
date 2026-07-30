using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Workers.NotificationDispatcher.Implementation;
using DM.Workers.NotificationDispatcher.Implementation.Notifiers;
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
    private const string DeadLetterExchangeName = "dm.notifications.undelivered";

    private readonly ILogger<NotificationDispatcherConsumer> _logger;
    private readonly IConsumerBuilder _consumerBuilder;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAsyncConnectionFactory _rabbitConnectionFactory;
    private readonly RetryPolicy _consumeRetryPolicy;

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
        _consumeRetryPolicy = Policy.Handle<Exception>().WaitAndRetry(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => _logger.LogWarning(exception, "Could not subscribe to the queue"));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[??] Starting notifications consumer");

        DeadLetterQueue.DeclareTerminal(_rabbitConnectionFactory, DeadLetterExchangeName);

        var parameters = new RabbitConsumerParameters("dm.notifications", "dm.notifications", ProcessingOrder.Unmanaged)
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
        _consumeRetryPolicy.Execute(consumer.Subscribe);

        _logger.LogDebug("[??] Notifications consumer is listening to {QueueName} queue", parameters.QueueName);
        return Task.CompletedTask;
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
