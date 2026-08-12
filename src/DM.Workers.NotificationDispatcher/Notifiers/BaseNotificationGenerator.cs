using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Notifiers;

/// <inheritdoc />
public abstract class BaseNotificationGenerator : INotificationGenerator
{
    /// <summary>
    /// Event type that generator can process
    /// </summary>
    protected abstract EventType EventType { get; }

    /// <inheritdoc />
    public bool CanResolve(EventType eventType) => eventType == EventType;

    /// <summary>
    /// Generate DAL models of notifications to be stored
    /// </summary>
    /// <param name="entityId">Entity identifier</param>
    /// <returns>Async enumerable of notifications to create</returns>
    public abstract IAsyncEnumerable<CreateNotification> Generate(Guid entityId);
}
