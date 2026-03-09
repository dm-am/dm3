using DM.Domain.Core.Events;

namespace DM.Infrastructure.Messaging.GeneralBus;

/// <summary>
/// Produces domain events to the message bus.
/// Extends <see cref="IEventProducer"/> for backward compatibility.
/// </summary>
/// <remarks>
/// DEPRECATED: Use <see cref="IEventProducer"/> from Domain.Core.Events instead.
/// This interface will be removed after migration is complete.
/// </remarks>
public interface IInvokedEventProducer : IEventProducer;