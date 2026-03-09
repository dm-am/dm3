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
/// Implements both <see cref="IEventProducer"/> (new) and <see cref="IInvokedEventProducer"/> (deprecated).
/// </summary>
internal class InvokedEventProducer(IProducerBuilder producerBuilder) : IEventProducer, IInvokedEventProducer
{
    private readonly IProducer<string, InvokedEvent> producer = producerBuilder.BuildRabbit<InvokedEvent>(
        new RabbitProducerParameters(InvokedEventsTransport.ExchangeName));

    public Task Send(EventType eventType, Guid entityId) =>
        producer.Send(GetRoutingKey(eventType), new InvokedEvent
        {
            Type = eventType,
            EntityId = entityId
        }, CancellationToken.None);

    public async Task Send(IEnumerable<EventType> eventTypes, Guid entityId)
    {
        foreach (var eventType in eventTypes)
        {
            await Send(eventType, entityId);
        }
    }

    private static string GetRoutingKey(EventType eventType)
    {
        var name = Enum.GetName(eventType.GetType(), eventType) ??
                   throw new InvokedEventException($"??????????? ??? ???????????? {eventType}");
        var field = eventType.GetType().GetField(name) ??
                    throw new InvokedEventException($"?? ??????? ???? ???????????? {eventType}");
        var attribute = Attribute.GetCustomAttribute(field, typeof(EventRoutingKeyAttribute));
        return attribute is EventRoutingKeyAttribute eventRoutingKeyAttribute
            ? eventRoutingKeyAttribute.RoutingKey
            : throw new InvokedEventException($"??????????? ???????? {nameof(EventRoutingKeyAttribute)}");
    }
}