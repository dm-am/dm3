using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Infrastructure.Messaging.Queues;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Workers.NotificationDispatcher;

/// <summary>
/// The loop of the notification worker over the notification queue.
/// </summary>
/// <remarks>
/// Every event type this build knows is claimed, the ones no generator handles included:
/// such an event costs one claim and is marked processed. A type the build does not know,
/// stored by a newer API, waits for a worker that knows it.
/// </remarks>
internal static class NotificationQueue
{
    /// <summary>The event types of this build, as the table stores them.</summary>
    public static int[] KnownEventTypes() => Enum.GetValues<EventType>().Select(type => (int)type).ToArray();

    /// <summary>Builds the loop from the services of the process.</summary>
    public static QueueWorker Worker(IServiceProvider services) =>
        QueueWorker.FromServices(services, QueueNames.Notifications, KnownEventTypes(), Handle);

    /// <summary>
    /// Hands the event of a row to the unchanged event handler, under the queue's name, which
    /// the processor takes as its source.
    /// </summary>
    /// <remarks>
    /// A row without its event type or entity holds no event, and no retry would give it one:
    /// the answer is that it cannot be processed, which makes the row dead at once.
    /// </remarks>
    public static Task<ProcessResult> Handle(OutboxRow row, IServiceProvider scope, CancellationToken cancellationToken) =>
        row is { EventType: { } eventType, EntityId: { } entityId }
            ? scope.GetRequiredService<IProcessor<string, InvokedEvent>>().Process(
                QueueNames.Notifications,
                new InvokedEvent
                {
                    Type = (EventType)eventType,
                    EntityId = entityId,
                    EventId = row.EventId,
                },
                cancellationToken)
            : Task.FromResult(ProcessResult.Failure);
}
