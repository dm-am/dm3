using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Messaging.GeneralBus;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;

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
    private readonly IProducer<string, RealtimeNotification> producer;

    /// <inheritdoc />
    public RealtimeNotificationProducer(IProducerBuilder producerBuilder) =>
        producer = producerBuilder.BuildRabbit<RealtimeNotification>(
            new RabbitProducerParameters(RealtimeNotificationsTransport.ExchangeName));

    /// <inheritdoc />
    public Task SendAsync(RealtimeNotification notification, CancellationToken cancellationToken) =>
        producer.Send(string.Empty, notification, cancellationToken);

    /// <summary>
    /// Returns the AMQP channel this producer took from the pool. Same reasoning as
    /// InvokedEventProducer and MailSender: the producer leases a channel on its
    /// first send and gives it back only on Dispose.
    /// </summary>
    public void Dispose() => (producer as IDisposable)?.Dispose();
}
