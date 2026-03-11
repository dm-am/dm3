using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Workers.NotificationDispatcher.Implementation;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Rabbit.Consuming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DM.Workers.NotificationDispatcher;

internal class NotificationDispatcherConsumer : BackgroundService
{
    private readonly ILogger<NotificationDispatcherConsumer> _logger;
    private readonly IConsumerBuilder _consumerBuilder;
    private readonly RetryPolicy _consumeRetryPolicy;

    public NotificationDispatcherConsumer(
        ILogger<NotificationDispatcherConsumer> logger,
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
        _logger.LogDebug("[??] Starting notifications consumer");

        var parameters = new RabbitConsumerParameters("dm.notifications", "dm.notifications", ProcessingOrder.Unmanaged)
        {
            ExchangeName = InvokedEventsTransport.ExchangeName,
            RoutingKeys = new[]
            {
                // Community
                EventType.ActivatedUser,

                // Forum
                EventType.NewTopicComment,
                EventType.ChangedTopicComment,
                EventType.DeletedTopicComment,
                EventType.LikedTopicComment,
                EventType.NewTopic,
                EventType.ChangedTopic,
                EventType.DeletedTopic,
                EventType.LikedTopic,

                // Blog
                EventType.LikedPublication,
                EventType.LikedBlogComment,
                EventType.LikedPublicationComment,

                // Game comments
                EventType.LikedGameComment,

                // Messaging
                EventType.LikedMessage,

                // Game status changes
                EventType.StatusGameActive,
                EventType.StatusGameClosed,
                EventType.StatusGameFrozen,
                EventType.StatusGameFinished,
                EventType.GameClosureWarning,
                EventType.GameRecruitmentOpened,

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
                EventType.StatusCharacterExiled,
                EventType.StatusCharacterRetired,

                // Pendency
                EventType.RoomPendencyCreated,

                // Posts
                EventType.PostReviewed
            }.ToRoutingKeys(),
        };
        var consumer = _consumerBuilder.BuildRabbit<InvokedEvent, NotificationProcessor>(parameters);
        _consumeRetryPolicy.Execute(consumer.Subscribe);

        _logger.LogDebug("[??] Notifications consumer is listening to {QueueName} queue", parameters.QueueName);
        return Task.CompletedTask;
    }
}