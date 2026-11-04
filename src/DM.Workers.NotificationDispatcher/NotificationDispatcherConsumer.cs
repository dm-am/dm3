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
        _consumeRetryPolicy = BrokerRetry.Create(exception =>
            _logger.LogWarning(exception, "Could not subscribe to the queue"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[🚴] Starting notifications consumer");

        // Everything before the first await runs inside host startup.
        await Task.Yield();

        var parameters = new DmConsumerParameters(QueueName, QueueName)
        {
            ExchangeName = InvokedEventsTransport.ExchangeName,
            RoutingKeys = ResolveHandledEventTypes().ToRoutingKeys().ToArray(),

            DeadLetterExchange = DeadLetterExchangeName,
        };
        var consumer = _consumerBuilder.Build<InvokedEvent, NotificationProcessor>(parameters);

        // The dead-letter declaration talks to the same broker, so it retries with the subscription.
        await _consumeRetryPolicy.ExecuteAsync(async token =>
        {
            await DeadLetterQueue.DeclareTerminal(_brokerConnection, DeadLetterExchangeName, token);
            await consumer.Subscribe(token);
        }, stoppingToken);

        _logger.LogDebug("[👂] Notifications consumer is listening to {QueueName} queue", parameters.QueueName);
    }

    /// <summary>
    /// The queue binds exactly the events some generator declares it can handle.
    /// </summary>
    /// <remarks>
    /// Derived, because a hand-kept list starves a generator silently. Resolved in a scope released at once.
    /// No alert on the unroutable-return counter: events no generator handles keep it nonzero on every stand.
    /// </remarks>
    private EventType[] ResolveHandledEventTypes()
    {
        using var scope = _serviceProvider.CreateScope();
        var generators = scope.ServiceProvider.GetRequiredService<IEnumerable<INotificationGenerator>>().ToArray();
        return Enum.GetValues<EventType>()
            .Where(eventType => generators.Any(generator => generator.CanResolve(eventType)))
            .ToArray();
    }
}
