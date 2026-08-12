using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Domain.Personal.Features.Notifications;
using DM.Workers.NotificationDispatcher.Implementation.Notifiers;
using DM.Workers.NotificationDispatcher.Implementation.Email;
using DM.Workers.NotificationDispatcher.Implementation.Bot;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.Extensions.Logging;

namespace DM.Workers.NotificationDispatcher.Implementation;

/// <inheritdoc />
internal class NotificationProcessor : IProcessor<string, InvokedEvent>
{
    private readonly IEnumerable<INotificationGenerator> _generators;
    private readonly INotificationService _notificationService;
    private readonly INotificationEmailSender _emailSender;
    private readonly INotificationBotSender _botSender;
    private readonly IMapper _mapper;
    private readonly IRealtimeNotificationProducer _producer;
    private readonly ILogger<NotificationProcessor> _logger;

    /// <inheritdoc />
    public NotificationProcessor(
        IEnumerable<INotificationGenerator> generators,
        INotificationService notificationService,
        INotificationEmailSender emailSender,
        INotificationBotSender botSender,
        IMapper mapper,
        IRealtimeNotificationProducer producer,
        ILogger<NotificationProcessor> logger)
    {
        _generators = generators;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _botSender = botSender;
        _mapper = mapper;
        _producer = producer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProcessResult> Process(string key, InvokedEvent message, CancellationToken cancellationToken)
    {
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
                notificationsToCreate.Add(createNotification with { EventType = eventType });
            }
        }

        if (!notificationsToCreate.Any())
        {
            return ProcessResult.Success;
        }

        var notifications = await _notificationService.CreateAsync(notificationsToCreate, cancellationToken);

        // Past this call the notifications are durable, and the event carries no
        // idempotency key — nothing downstream can tell a replay from a first
        // delivery. An exception escaping from here hands the whole method back to
        // the retry middleware, which repeats the write as well: the recipient ends
        // up with the same entry twice in the list, two letters and two bot
        // messages. So delivery is best effort, logged and dropped on failure. The
        // cost is bounded — the stored notification is what the list is built from,
        // so a lost push only delays it until the next page load. Everything above
        // this line has no side effects and still throws, which is what lets a
        // message that produced nothing yet be replayed safely.
        //
        // What that leaves open is redelivery from the broker — a worker killed
        // between the write and the acknowledgement, a nack, a restart — which
        // hands the same message back and produces a second set of notifications.
        // Weighed and accepted 2026-08-12 rather than overlooked. Closing it needs
        // an idempotency key on InvokedEvent, filled by the single publishing point
        // and honoured by an idempotent write here; that is a change to the bus
        // contract every producer of events shares, on the path where a mistake
        // means notifications stop arriving at all. It earns its own pass, not a
        // rider on somebody else's.

        // In-app notifications, pushed over SignalR by the API
        foreach (var notification in notifications)
        {
            await Deliver("realtime", notification.Entity.EventType, () =>
                _producer.SendAsync(_mapper.Map<RealtimeNotification>(notification.Entity), cancellationToken));
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
            _logger.LogWarning(exception, "Failed to deliver notification of {EventType} over {Channel}",
                eventType, channel);
        }
    }
}
