using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Core.Extensions;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;

namespace DM.Infrastructure.Messaging.GeneralBus;

/// <summary>
/// Event producer implementation that sends domain events through RabbitMQ.
/// </summary>
internal class InvokedEventProducer(IProducerBuilder producerBuilder)
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

    public Task SendAsync(EventType eventType, Guid entityId) =>
        producer.Send(GetRoutingKey(eventType), new InvokedEvent
        {
            Type = eventType,
            EntityId = entityId
        }, CancellationToken.None);

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
