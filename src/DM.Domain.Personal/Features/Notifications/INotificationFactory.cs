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
    /// <param name="createNotification"></param>
    /// <param name="createDate">Common create date</param>
    /// <returns></returns>
    CreateNotificationEntity Create(CreateNotification createNotification, DateTimeOffset createDate);
}