using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;

namespace DM.Infrastructure.Core.Extensions;

/// <summary>
/// Extensions for invoked events
/// </summary>
public static class InvokedEventTypeExtensions
{
    /// <summary>
    /// Map event types to routing keys
    /// </summary>
    /// <param name="eventTypes">Event types</param>
    /// <returns></returns>
    public static IEnumerable<string> ToRoutingKeys(this IEnumerable<EventType> eventTypes) =>
        eventTypes.Select(eventType =>
            eventType.GetAttribute<EventType, EventRoutingKeyAttribute>().RoutingKey);
}
