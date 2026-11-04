using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Messaging;

/// <summary>
/// Generates a realtime broadcast notification when an event of the global chat
/// starts or ends.
/// </summary>
/// <remarks>
/// Nothing polls the strip above the feed. UsersInterested is empty: the API broadcasts to every connection.
/// </remarks>
internal class GlobalChatEventLifecycleNotificationGenerator : INotificationGenerator
{
    private static readonly EventType[] SupportedTypes =
    {
        EventType.GlobalChatEventStarted,
        EventType.GlobalChatEventEnded
    };

    private readonly DmDbContext _dbContext;

    public GlobalChatEventLifecycleNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public bool CanResolve(EventType eventType) => SupportedTypes.Contains(eventType);

    /// <inheritdoc />
    public async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        // Redelivered after the event has gone, the push is noise.
        var exists = await _dbContext.GlobalChatEvents
            .AnyAsync(e => e.GlobalChatEventId == entityId);

        if (!exists)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            // Empty: the API broadcasts to every connection
            UsersInterested = [],
            // No ActorId: the row does not record who started or ended the event.
            Metadata = new
            {
                GlobalChatEventId = entityId
            }
        };
    }
}
