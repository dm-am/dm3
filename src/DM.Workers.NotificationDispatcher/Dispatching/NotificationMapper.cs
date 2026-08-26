using DM.Domain.Personal.Features.Notifications;
using Riok.Mapperly.Abstractions;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// Compile-time mapper for the SignalR push right after CreateAsync - the
/// path that bypasses a second DB fetch. EventId stays behind: it identifies
/// the bus publication for redelivery checks and means nothing to a client.
/// </summary>
[Mapper]
internal static partial class NotificationMapper
{
    /// <summary>
    /// Stored-notification create model to the realtime push payload
    /// </summary>
    [MapProperty(nameof(CreateNotificationEntity.UsersInterested), nameof(RealtimeNotification.RecipientIds))]
    [MapperIgnoreSource(nameof(CreateNotificationEntity.EventId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial RealtimeNotification ToRealtimeNotification(this CreateNotificationEntity entity);
}
