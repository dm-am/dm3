using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers;

/// <summary>
/// Generates notification for certain event
/// </summary>
internal interface INotificationGenerator
{
    /// <summary>
    /// Tells if resolver can process the certain event type
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <returns></returns>
    bool CanResolve(EventType eventType);

    /// <summary>
    /// Generate notifications off the event
    /// </summary>
    /// <param name="entityId">Entity identifier</param>
    /// <returns></returns>
    IAsyncEnumerable<CreateNotification> Generate(Guid entityId);
}