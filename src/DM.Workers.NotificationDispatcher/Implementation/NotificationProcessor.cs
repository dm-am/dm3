using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Domain.Personal.Features.Notifications;
using DM.Workers.NotificationDispatcher.Implementation.Notifiers;
using DM.Workers.NotificationDispatcher.Implementation.Email;
using DM.Workers.NotificationDispatcher.Implementation.Bot;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;

namespace DM.Workers.NotificationDispatcher.Implementation;

/// <inheritdoc />
internal class NotificationProcessor : IProcessor<string, InvokedEvent>
{
    private readonly IEnumerable<INotificationGenerator> _generators;
    private readonly INotificationService _notificationService;
    private readonly INotificationEmailSender _emailSender;
    private readonly INotificationBotSender _botSender;
    private readonly IMapper _mapper;
    private readonly IProducer<string, RealtimeNotification> _producer;

    /// <inheritdoc />
    public NotificationProcessor(
        IEnumerable<INotificationGenerator> generators,
        INotificationService notificationService,
        INotificationEmailSender emailSender,
        INotificationBotSender botSender,
        IMapper mapper,
        IProducerBuilder producerBuilder)
    {
        _generators = generators;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _botSender = botSender;
        _mapper = mapper;
        _producer = producerBuilder.BuildRabbit<RealtimeNotification>(
            new RabbitProducerParameters("dm.notifications.sent"));
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

        var notifications = await _notificationService.CreateAsync(notificationsToCreate);

        // Send to SignalR (in-app notifications)
        foreach (var notification in notifications.Select(_mapper.Map<RealtimeNotification>))
        {
            await _producer.Send(string.Empty, notification, cancellationToken);
        }

        // Send email notifications to users who have enabled them
        foreach (var createNotification in notificationsToCreate)
        {
            await _emailSender.SendIfEnabled(createNotification, createNotification.EventType, cancellationToken);
        }

        // Send bot notifications (Discord/Telegram) to users who have enabled them
        foreach (var createNotification in notificationsToCreate)
        {
            await _botSender.SendIfEnabled(createNotification, createNotification.EventType, cancellationToken);
        }

        return ProcessResult.Success;
    }
}