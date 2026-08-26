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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DM.Workers.NotificationDispatcher;

internal class NotificationDispatcherConsumer : BackgroundService
{
    /// <summary>Queue this worker reads, as both the topology and the metrics name it.</summary>
    internal const string QueueName = "dm.notifications";

    private const string DeadLetterExchangeName = "dm.notifications.undelivered";

    private readonly ILogger<NotificationDispatcherConsumer> _logger;
    private readonly IDmConsumerBuilder _consumerBuilder;
    private readonly IServiceProvider _serviceProvider;
    private readonly DmBrokerConnection _brokerConnection;
    private readonly AsyncRetryPolicy _consumeRetryPolicy;

    public NotificationDispatcherConsumer(
        ILogger<NotificationDispatcherConsumer> logger,
        IDmConsumerBuilder consumerBuilder,
        IServiceProvider serviceProvider,
        DmBrokerConnection brokerConnection)
    {
        _logger = logger;
        _consumerBuilder = consumerBuilder;
        _serviceProvider = serviceProvider;
        _brokerConnection = brokerConnection;
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

        // The prefetch of one is not declared here because it is not a choice a
        // consumer gets to make: DmConsumer sets it for every subscription, and
        // that file says why a worker allowed to take the whole queue builds a
        // backlog no rule can see.
        var parameters = new DmConsumerParameters("dm.notifications", QueueName)
        {
            ExchangeName = InvokedEventsTransport.ExchangeName,
            RoutingKeys = ResolveHandledEventTypes().ToRoutingKeys().ToArray(),

            // Without this the queue has no dead-letter exchange, so an event a
            // generator keeps throwing on is rejected into nothing: the retries run
            // out, the exception escapes the middleware, and the broker drops the
            // message. The recipient is never told, and all that is left of the
            // event is five warnings in the log.
            DeadLetterExchange = DeadLetterExchangeName,
        };
        var consumer = _consumerBuilder.Build<InvokedEvent, NotificationProcessor>(parameters);

        // The dead-letter declaration is inside the policy with the subscription:
        // both talk to the same broker, and one call left outside the policy is
        // the one an unreachable broker throws past entirely.
        await _consumeRetryPolicy.ExecuteAsync(async token =>
        {
            await DeadLetterQueue.DeclareTerminal(_brokerConnection, DeadLetterExchangeName, token);
            await consumer.Subscribe(token);
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
    ///
    /// The price of deriving it: an event type no generator handles matches no
    /// binding, so the broker returns it to the publisher as unroutable and the
    /// client drops the return without a word. That counter is expected to be
    /// nonzero on every stand from the first minute, and no threshold on it means
    /// anything - which is why nothing alerts on it.
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
