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
        // Empty is a message without the key: stamped through, every such message would be one replayed event.
        var eventId = message.EventId == Guid.Empty ? (Guid?)null : message.EventId;

        var notificationsToCreate = new List<CreateNotification>();
        foreach (var generator in _generators.Where(g => g.CanResolve(message.Type)))
        {
            await foreach (var createNotification in generator.Generate(message.EntityId)
                               .WithCancellation(cancellationToken))
            {
                // Subscription generators set their own output EventType.
                var eventType = createNotification.EventType != default
                    ? createNotification.EventType
                    : message.Type;
                notificationsToCreate.Add(createNotification with { EventType = eventType, EventId = eventId });
            }
        }

        if (notificationsToCreate.Count == 0)
        {
            return ProcessResult.Success;
        }

        // Counted before the call: past it only the filtered answer may be read.
        var requestedCount = notificationsToCreate.Count;

        var notifications = await _notificationService.CreateAsync(notificationsToCreate, cancellationToken);

        // Past this call the notifications are durable, so delivery below is best effort.
        // A replay gets an answer without the stored notifications, so each channel delivers at most once.
        if (notifications.Count < requestedCount)
        {
            _logger.LogInformation(
                "Skipped {SkippedCount} of {TotalCount} notifications already stored for replayed event {EventId} of {EventType}",
                requestedCount - notifications.Count, requestedCount,
                message.EventId, message.Type);
        }

        foreach (var notification in notifications)
        {
            await Deliver("realtime", notification.Entity.EventType, () =>
                _producer.SendAsync(notification.Entity.ToRealtimeNotification(), cancellationToken));
        }

        // A realtime-only notification exists to refresh a counter in an open tab.
        // Read off the answer, not off notificationsToCreate: the service narrows the audience.
        var outbound = notifications
            .Select(n => n.Source)
            .Where(n => !n.RealtimeOnly)
            .ToArray();

        foreach (var createNotification in outbound)
        {
            await Deliver("email", createNotification.EventType, () =>
                _emailSender.SendIfEnabled(createNotification, createNotification.EventType, cancellationToken));
        }

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
