using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.MessageQueuing.GeneralBus;
using DM.Services.Notifications.Consumer.Implementation;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Rabbit.Consuming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DM.Services.Notifications.Consumer;

internal class NotificationConsumer : BackgroundService
{
    private readonly ILogger<NotificationConsumer> _logger;
    private readonly IConsumerBuilder _consumerBuilder;
    private readonly RetryPolicy _consumeRetryPolicy;

    public NotificationConsumer(
        ILogger<NotificationConsumer> logger,
        IConsumerBuilder consumerBuilder)
    {
        _logger = logger;
        _consumerBuilder = consumerBuilder;
        _consumeRetryPolicy = Policy.Handle<Exception>().WaitAndRetry(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => _logger.LogWarning(exception, "Could not subscribe to the queue"));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[🚴] Starting notifications consumer");

        var parameters = new RabbitConsumerParameters("dm.notifications", "dm.notifications", ProcessingOrder.Unmanaged)
        {
            ExchangeName = InvokedEventsTransport.ExchangeName,
            RoutingKeys = new[]
            {
                // Community
                EventType.ActivatedUser,

                // Forum
                EventType.NewForumComment,
                EventType.ChangedForumComment,
                EventType.DeletedForumComment,
                EventType.LikedForumComment,
                EventType.NewForumTopic,
                EventType.ChangedForumTopic,
                EventType.DeletedForumTopic,
                EventType.LikedTopic,

                // Game status changes
                EventType.StatusGameActive,
                EventType.StatusGameClosed,
                EventType.StatusGameFrozen,
                EventType.StatusGameFinished,

                // Invitations
                EventType.AssignmentRequestCreated,
                EventType.PlayerInvitationCreated,
                EventType.ReaderInvitationCreated,

                // Characters
                EventType.NewCharacter,
                EventType.StatusCharacterDeclined,
                EventType.StatusCharacterAccepted,
                EventType.StatusCharacterDied,
                EventType.StatusCharacterResurrected,
                EventType.StatusCharacterLeft,
                EventType.StatusCharacterReturned,

                // Posts
                EventType.PostVoted
            }.ToRoutingKeys(),
        };
        var consumer = _consumerBuilder.BuildRabbit<InvokedEvent, NotificationProcessor>(parameters);
        _consumeRetryPolicy.Execute(consumer.Subscribe);

        _logger.LogDebug("[👂] Notifications consumer is listening to {QueueName} queue", parameters.QueueName);
        return Task.CompletedTask;
    }
}