using System.Linq;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Web.API.Features.Personal.Notifications;

/// <summary>
/// One notification into the DTO both transports write: the list over REST and the
/// push over the hub.
/// </summary>
/// <remarks>
/// One builder, so both transports carry the same fields (NotificationWireShould
/// in DM.Architecture.Tests holds the push to it). The words, the path and the
/// details come from the tables the letter is written from; the path is relative.
/// </remarks>
internal static class NotificationMapper
{
    /// <summary>
    /// Notification to its DTO
    /// </summary>
    /// <param name="notification">Notification, read from the store or taken off the bus</param>
    /// <param name="isRead">Whether the reader has read it</param>
    public static Notification ToNotification(UserNotification notification, bool isRead) => new()
    {
        Id = notification.NotificationId,
        EventType = notification.EventType,
        CreatedUtc = notification.CreatedUtc,
        IsRead = isRead,
        Title = NotificationWording.GetTitle(notification.EventType),
        Url = NotificationDestination.GetPath(notification.EventType, notification.Metadata),
        Details = NotificationWording.ReadDetails(
                notification.Metadata, NotificationDestination.PlaceFieldOf(notification.EventType))
            .Select(detail => new NotificationDetail
            {
                Label = detail.Label,
                Value = detail.Value,
                IsMoment = detail.IsMoment,
                IsDestination = detail.IsDestination
            })
            .ToArray(),
        Payload = notification.Metadata
    };

    /// <summary>
    /// A row of the reader's list to its DTO
    /// </summary>
    /// <param name="notification">Notification with the reader's mark</param>
    public static Notification ToNotification(ListedNotification notification) =>
        ToNotification(notification, notification.IsRead);
}
