using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;

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
internal class RealtimeNotificationProducer : IRealtimeNotificationProducer, IDisposable
{
    private readonly IDmProducer<RealtimeNotification> producer;

    /// <inheritdoc />
    public RealtimeNotificationProducer(IDmProducerBuilder producerBuilder) =>
        producer = producerBuilder.Build<RealtimeNotification>(
            new DmProducerParameters(RealtimeNotificationsTransport.ExchangeName));

    /// <inheritdoc />
    public Task SendAsync(RealtimeNotification notification, CancellationToken cancellationToken) =>
        producer.Send(string.Empty, notification, cancellationToken);

    /// <summary>
    /// Closes the AMQP channel this producer opened. Same reasoning as
    /// MailSender: the producer opens a channel on its first send and closes it
    /// only on Dispose.
    /// </summary>
    public void Dispose() => producer.Dispose();
}
