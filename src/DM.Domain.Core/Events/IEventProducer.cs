using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Events;

/// <summary>
/// Produces domain events to the message bus
/// </summary>
public interface IEventProducer
{
    Task SendAsync(EventType eventType, Guid entityId);

    Task SendAsync(IEnumerable<EventType> eventTypes, Guid entityId);
}
