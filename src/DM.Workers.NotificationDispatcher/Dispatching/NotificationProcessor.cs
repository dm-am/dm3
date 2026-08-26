using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Tracing;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Domain.Personal.Features.Notifications;
using DM.Workers.NotificationDispatcher.Notifiers;
using DM.Workers.NotificationDispatcher.Email;
using DM.Workers.NotificationDispatcher.Bot;
using Microsoft.Extensions.Logging;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <inheritdoc />
internal class NotificationProcessor : IProcessor<string, InvokedEvent>
{
    private readonly IEnumerable<INotificationGenerator> _generators;
    private readonly INotificationService _notificationService;
    private readonly INotificationEmailSender _emailSender;
    private readonly INotificationBotSender _botSender;
    private readonly IRealtimeNotificationProducer _producer;
    private readonly ILogger<NotificationProcessor> _logger;

    /// <inheritdoc />
    public NotificationProcessor(
        IEnumerable<INotificationGenerator> generators,
        INotificationService notificationService,
        INotificationEmailSender emailSender,
        INotificationBotSender botSender,
        IRealtimeNotificationProducer producer,
        ILogger<NotificationProcessor> logger)
    {
        _generators = generators;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _botSender = botSender;
        _producer = producer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProcessResult> Process(string key, InvokedEvent message, CancellationToken cancellationToken)
    {
        // Empty is a message queued before the bus carried an idempotency key:
        // stamping it through would make every legacy message a replay of one
        // and the same event, so such a message is processed the way it always
        // had been - written and delivered without deduplication.
        var eventId = message.EventId == Guid.Empty ? (Guid?)null : message.EventId;

        var notificationsToCreate = new List<CreateNotification>();
        foreach (var generator in _generators.Where(g => g.CanResolve(message.Type)))
        {
            await foreach (var createNotification in generator.Generate(message.EntityId)
                               .WithCancellation(cancellationToken))
            {
                // If generator didn't set EventType, use the message type
                // Subscription generators set their own output EventType
                var eventType = createNotification.EventType != default
                    ? createNotification.EventType
                    : message.Type;
                notificationsToCreate.Add(createNotification with { EventType = eventType, EventId = eventId });
            }
        }

        if (!notificationsToCreate.Any())
        {
            return ProcessResult.Success;
        }

        // Counted before the creation call: past it the requested list may not be
        // read at all - NotificationRecipientsShould holds every channel to the
        // filtered answer - and the count is the one thing the replay log needs.
        var requestedCount = notificationsToCreate.Count;

        var notifications = await _notificationService.CreateAsync(notificationsToCreate, cancellationToken);

        // Past this call the notifications are durable. Delivery below is best
        // effort, logged and dropped on failure: an exception escaping from here
        // would hand the whole method back to the retry middleware, and the cost
        // of a lost push is bounded — the stored notification is what the list is
        // built from, so it only delays until the next page load.
        //
        // A broker redelivery — a worker killed between the write and the
        // acknowledgement, a nack, a restart — hands the same message in again,
        // and since W1.4 that is safe end to end: the event carries an EventId
        // from the single publishing point, CreateAsync writes idempotently by
        // (EventId, EventType) under a unique index, and a replay gets an answer
        // with the already-stored notifications removed. The channels below all
        // iterate that answer, so what was written once is also mailed, botted
        // and pushed at most once. The exceptions are deliberate: a message with
        // no EventId predates the key and flows through undeduplicated, and a
        // realtime-only notification has no row to find, so a replay repeats a
        // push whose whole effect is an open tab re-reading a counter.
        if (notifications.Count < requestedCount)
        {
            _logger.LogInformation(
                "Skipped {SkippedCount} of {TotalCount} notifications already stored for replayed event {EventId} of {EventType}",
                requestedCount - notifications.Count, requestedCount,
                message.EventId, message.Type);
        }

        // In-app notifications, pushed over SignalR by the API
        foreach (var notification in notifications)
        {
            await Deliver("realtime", notification.Entity.EventType, () =>
                _producer.SendAsync(notification.Entity.ToRealtimeNotification(), cancellationToken));
        }

        // A realtime-only notification ends at the hub. It carries no words of
        // its own and exists to refresh a counter in an open tab, so mailing it
        // out would turn a badge into correspondence nobody asked for.
        //
        // Read off what CreateAsync answered, never off notificationsToCreate:
        // the service narrows the audience to who may receive the notification,
        // and the list built above is the one it was asked for. Mailing that one
        // delivers precisely what the filter refused to store.
        var outbound = notifications
            .Select(n => n.Source)
            .Where(n => !n.RealtimeOnly)
            .ToArray();

        // Email notifications to users who have enabled them
        foreach (var createNotification in outbound)
        {
            await Deliver("email", createNotification.EventType, () =>
                _emailSender.SendIfEnabled(createNotification, createNotification.EventType, cancellationToken));
        }

        // Bot notifications (Discord/Telegram) to users who have enabled them
        foreach (var createNotification in outbound)
        {
            await Deliver("bot", createNotification.EventType, () =>
                _botSender.SendIfEnabled(createNotification, createNotification.EventType, cancellationToken));
        }

        return ProcessResult.Success;
    }

    /// <summary>
    /// Runs one delivery and keeps its failure to itself, so that a channel which is
    /// down cannot cost the recipient duplicates over the channels which are up.
    /// </summary>
    private async Task Deliver(string channel, EventType eventType, Func<Task> deliver)
    {
        try
        {
            await deliver();
        }
        catch (Exception exception)
        {
            MessagingMetrics.DeliveryFailed.Add(1,
                MessagingMetrics.Channel(channel),
                new KeyValuePair<string, object?>("event", eventType.ToString()));
            _logger.LogWarning(exception, "Failed to deliver notification of {EventType} over {Channel}",
                eventType, channel);
        }
    }
}
