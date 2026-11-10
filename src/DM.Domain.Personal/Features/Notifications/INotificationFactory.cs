using System;

namespace DM.Domain.Personal.Features.Notifications;

internal interface INotificationFactory
{
    CreateNotificationEntity Create(CreateNotification createNotification, DateTimeOffset createDate);
}
