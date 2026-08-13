using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Tracing;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Messaging.GeneralBus;

/// <summary>
/// Event producer implementation that sends domain events through RabbitMQ.
/// </summary>
internal class InvokedEventProducer(
    IProducerBuilder producerBuilder,
    ILogger<InvokedEventProducer> logger)
    : IEventProducer, IDisposable
{
    private readonly IProducer<string, InvokedEvent> producer = producerBuilder.BuildRabbit<InvokedEvent>(
        new RabbitProducerParameters(InvokedEventsTransport.ExchangeName));

    /// <summary>
    /// Returns the AMQP channel this producer took from the pool.
    /// </summary>
    /// <remarks>
    /// BuildRabbit hands back a RabbitProducer that leases a channel on its first
    /// Send and gives it back only on Dispose — the pool has a Get and no Return.
    /// This wrapper was not disposable and was registered per dependency by the
    /// blanket scan, so every resolution that published anything left a channel
    /// open for the life of the process. With the shipped defaults that is 16
    /// pools of 256, so after about four thousand published events the pool throws
    /// and every publish in the API fails until it is restarted: no search
    /// indexing, no notifications, no mail.
    ///
    /// Disposing here rather than making the producer a singleton: a RabbitMQ
    /// channel is not safe to publish on from several threads at once, and a
    /// singleton would share one across every concurrent request. Registered per
    /// lifetime scope instead, so a request opens at most one and returns it when
    /// the scope ends.
    /// </remarks>
    public void Dispose() => (producer as IDisposable)?.Dispose();

    /// <inheritdoc />
    /// <remarks>
    /// A refusal is logged and counted, never thrown. The write that produced the
    /// event is committed by the time this runs, so a throw would turn a saved
    /// comment into a 500 and the caller would post it twice; SYSTEM.md already
    /// says an event is not the carrier of the fact, so the cheaper loss is the
    /// event. The rule lives here because the alternative is the same try/catch
    /// copied into every one of the seventy-odd call sites — and it was written
    /// in three of them.
    /// </remarks>
    public async Task SendAsync(EventType eventType, Guid entityId)
    {
        try
        {
            await producer.Send(GetRoutingKey(eventType), new InvokedEvent
            {
                Type = eventType,
                EntityId = entityId
            }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            MessagingMetrics.PublishFailed.Add(1,
                new KeyValuePair<string, object?>("event", eventType.ToString()),
                new KeyValuePair<string, object?>("reason", exception.GetType().Name));
            logger.LogWarning(exception,
                "Failed to publish {EventType} for {EntityId}", eventType, entityId);
        }
    }

    public async Task SendAsync(IEnumerable<EventType> eventTypes, Guid entityId)
    {
        foreach (var eventType in eventTypes)
        {
            await SendAsync(eventType, entityId);
        }
    }

    private static string GetRoutingKey(EventType eventType)
    {
        var name = Enum.GetName(eventType.GetType(), eventType) ??
                   throw new InvokedEventException($"Unknown enum name for {eventType}");
        var field = eventType.GetType().GetField(name) ??
                    throw new InvokedEventException($"Field not found for enum {eventType}");
        var attribute = Attribute.GetCustomAttribute(field, typeof(EventRoutingKeyAttribute));
        return attribute is EventRoutingKeyAttribute eventRoutingKeyAttribute
            ? eventRoutingKeyAttribute.RoutingKey
            : throw new InvokedEventException($"Missing {nameof(EventRoutingKeyAttribute)} attribute");
    }
}
