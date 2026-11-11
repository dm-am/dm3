using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence.Pushes;
using Npgsql;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// Sends the push of a notification for the API to deliver over SignalR.
/// </summary>
internal interface IRealtimeNotificationProducer
{
    /// <summary>Sends the push of one notification.</summary>
    /// <param name="notification">Notification to push</param>
    Task SendAsync(RealtimeNotification notification, CancellationToken cancellationToken);
}

/// <inheritdoc />
/// <remarks>
/// Through database notifications to the listener of every API instance.
/// </remarks>
internal class RealtimeNotificationProducer(NpgsqlDataSource dataSource) : IRealtimeNotificationProducer
{
    /// <inheritdoc />
    public Task SendAsync(RealtimeNotification notification, CancellationToken cancellationToken) =>
        PushSender.SendAsync(dataSource, notification, cancellationToken);
}
