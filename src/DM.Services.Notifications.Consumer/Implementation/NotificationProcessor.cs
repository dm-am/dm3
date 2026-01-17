using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.MessageQueuing.GeneralBus;
using DM.Services.Notifications.BusinessProcesses.Creating;
using DM.Services.Notifications.Consumer.Implementation.Notifiers;
using DM.Services.Notifications.Dto;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;

namespace DM.Services.Notifications.Consumer.Implementation;

/// <inheritdoc />
internal class NotificationProcessor : IProcessor<string, InvokedEvent>
{
    private readonly IEnumerable<INotificationGenerator> _generators;
    private readonly INotificationCreatingService _service;
    private readonly IMapper _mapper;
    private readonly IProducer<string, RealtimeNotification> _producer;

    /// <inheritdoc />
    public NotificationProcessor(
        IEnumerable<INotificationGenerator> generators,
        INotificationCreatingService service,
        IMapper mapper,
        IProducerBuilder producerBuilder)
    {
        _generators = generators;
        _service = service;
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
                notificationsToCreate.Add(createNotification with { EventType = message.Type });
            }
        }

        if (!notificationsToCreate.Any())
        {
            return ProcessResult.Success;
        }

        var notifications = await _service.Create(notificationsToCreate);
        foreach (var notification in notifications.Select(_mapper.Map<RealtimeNotification>))
        {
            await _producer.Send(string.Empty, notification, cancellationToken);
        }

        return ProcessResult.Success;
    }
}