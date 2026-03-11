using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Events;

/// <summary>
/// Produces domain events to the message bus
/// </summary>
public interface IEventProducer
{
    /// <summary>
    /// Sends an event to the message bus
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <param name="entityId">Entity identifier</param>
    /// <returns>Task</returns>
    Task SendAsync(EventType eventType, Guid entityId);

    /// <summary>
    /// Sends multiple events to the message bus
    /// </summary>
    /// <param name="eventTypes">Event types</param>
    /// <param name="entityId">Entity identifier</param>
    /// <returns>Task</returns>
    Task SendAsync(IEnumerable<EventType> eventTypes, Guid entityId);
}
