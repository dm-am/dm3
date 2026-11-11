using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence.Pushes;
using Npgsql;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// Publishes a notification for the API to push over SignalR.
/// </summary>
internal interface IRealtimeNotificationProducer
{
    /// <summary>
    /// Publish one notification
    /// </summary>
    /// <param name="notification">Notification to push</param>
    /// <param name="cancellationToken">Cancellation token</param>
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
