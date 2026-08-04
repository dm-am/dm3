using System;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// Factory for notification entity DTOs
/// </summary>
internal interface INotificationFactory
{
    /// <summary>
    /// Create new notification entity DTO
    /// </summary>
    /// <param name="createNotification">Notification creation data</param>
    /// <param name="createDate">Common create date</param>
    /// <returns>Notification entity DTO</returns>
    CreateNotificationEntity Create(CreateNotification createNotification, DateTimeOffset createDate);
}
