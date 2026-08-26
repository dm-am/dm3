using System;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Personal.Features.Notifications;

/// <inheritdoc />
internal class NotificationFactory : INotificationFactory
{
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public NotificationFactory(
        IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public CreateNotificationEntity Create(CreateNotification createNotification, DateTimeOffset createDate) => new()
    {
        NotificationId = _guidFactory.Create(),
        CreatedUtc = createDate,
        EventType = createNotification.EventType,
        EventId = createNotification.EventId,
        UsersInterested = createNotification.UsersInterested,
        Metadata = createNotification.Metadata
    };
}
