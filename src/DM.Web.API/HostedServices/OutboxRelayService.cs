using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Events;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Tracing;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Publishes stored domain events to the bus: the transport half of the
/// transactional outbox.
/// </summary>
/// <remarks>
/// Hosted in the API rather than in a worker on purpose: the outbox is written
/// by this process's requests, so the relay lives and dies with the writer -
/// hanging the producing half on a consuming worker would make delivery depend
/// on somebody else's deployment. The exchange is already declared by this
/// host, and every other background processor already runs here.
///
/// The one producer of this process for the events exchange, built once in the
/// constructor and closed in Dispose - one long-lived channel instead of the
/// channel-per-scope the direct producer used to open. It waits for a publisher
/// confirm, unlike the rest of the bus: not because the message is the whole
/// obligation - the outbox row is - but because marking the row published
/// without the broker's confirm would be recording "delivered" about a message
/// the broker may not have taken. The wait is affordable here because the relay
/// is background work: the rule "nobody waits on the bus" protected an HTTP
/// caller this loop does not have.
///
/// A second on the timer is the whole latency the outbox adds to the happy
/// path, realtime badge included; each tick keeps draining while batches come
/// back full, so a backlog accumulated behind a broker restart leaves in
/// hundreds per second rather than one batch per second.
/// </remarks>
internal class OutboxRelayService : PeriodicHostedService
{
    private readonly ILogger<OutboxRelayService> _logger;
    private readonly IDmProducer<InvokedEvent> _producer;

    public OutboxRelayService(
        IServiceProvider serviceProvider,
        ILogger<OutboxRelayService> logger,
        IDmProducerBuilder producerBuilder,
        IOptions<RabbitMqConfiguration> brokerConfiguration)
        : base(serviceProvider, logger)
    {
        _logger = logger;
        _producer = producerBuilder.Build<InvokedEvent>(
            new DmProducerParameters(InvokedEventsTransport.ExchangeName)
            {
                PublishConfirmTimeout = brokerConfiguration.Value.PublishConfirmTimeout,
            });
    }

    /// <inheritdoc />
    protected override string Tag => "[Outbox Relay]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var processor = scope.GetRequiredService<IOutboxRelayProcessor>();
        while (true)
        {
            var result = await processor.RelayBatchAsync(Publish, cancellationToken);

            MessagingMetrics.OutboxRelayPasses.Add(1);
            if (result.Published > 0)
            {
                MessagingMetrics.OutboxPublished.Add(result.Published);
            }

            MessagingMetrics.OutboxPending.Record(result.Pending);
            MessagingMetrics.OutboxLag.Record(result.OldestPendingAge.TotalSeconds);

            // Drain until the batch comes back short of full: a full one means
            // more may be waiting. A faulted batch stops the tick instead - the
            // refusal is almost always the broker being down, and the next tick
            // is the retry.
            if (result.Faulted || result.Published < IOutboxRelayProcessor.BatchSize)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Publishes one stored event with a publisher confirm. A throw means "the
    /// row did not leave" and stops the processor's batch; the row stays
    /// pending and the next pass retries it with the same EventId.
    /// </summary>
    private async Task Publish(OutboxEnvelope envelope, CancellationToken cancellationToken)
    {
        try
        {
            var routingKey = new[] { envelope.EventType }.ToRoutingKeys().Single();
            await _producer.Send(routingKey, new InvokedEvent
            {
                Type = envelope.EventType,
                EntityId = envelope.EntityId,
                EventId = envelope.EventId,
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A delay, not a loss: the row stays in the outbox and will be
            // retried. The irrecoverable losses stay on publish_failed.
            MessagingMetrics.OutboxPublishFailed.Add(1,
                new KeyValuePair<string, object?>("reason", exception.GetType().Name));
            _logger.LogWarning(exception,
                "[Outbox Relay] Failed to publish {EventType} for {EntityId}; the row stays pending",
                envelope.EventType, envelope.EntityId);
            throw;
        }
    }

    /// <summary>
    /// Closes the AMQP channel this relay opened. BackgroundService is already
    /// IDisposable, so the producer ownership rule holds without a second base.
    /// </summary>
    public override void Dispose()
    {
        _producer.Dispose();
        base.Dispose();
    }
}
