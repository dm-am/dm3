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
/// Hosted in the API, which writes the outbox, so delivery does not depend on a
/// worker's deployment. It waits for a publisher confirm, unlike the rest of the
/// bus: marking a row published without one would record a delivery the broker
/// may not have taken, and no HTTP caller waits on this loop.
/// </remarks>
internal class OutboxRelayService : PeriodicHostedService
{
    private readonly IDmProducer<InvokedEvent> _producer;

    public OutboxRelayService(
        IServiceProvider serviceProvider,
        ILogger<OutboxRelayService> logger,
        IDmProducerBuilder producerBuilder,
        IOptions<RabbitMqConfiguration> brokerConfiguration)
        : base(serviceProvider, logger)
    {
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

            // A full batch means more may be waiting. A faulted one is almost
            // always the broker being down, and the next tick is the retry.
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
            // A delay, not a loss: the row is retried, irrecoverable losses stay on publish_failed.
            MessagingMetrics.OutboxPublishFailed.Add(1,
                new KeyValuePair<string, object?>("reason", exception.GetType().Name));
            Logger.LogWarning(exception,
                "{Tag} Failed to publish {EventType} for {EntityId}; the row stays pending",
                Tag, envelope.EventType, envelope.EntityId);
            throw;
        }
    }

    /// <summary>
    /// Closes the AMQP channel this relay opened.
    /// </summary>
    public override void Dispose()
    {
        _producer.Dispose();
        base.Dispose();
    }
}
