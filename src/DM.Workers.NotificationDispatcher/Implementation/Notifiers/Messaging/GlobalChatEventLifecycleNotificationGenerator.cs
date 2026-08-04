using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Implementation.Notifiers.Messaging;

/// <summary>
/// Generates a realtime broadcast notification when an event of the global chat
/// starts or ends.
/// </summary>
/// <remarks>
/// The strip above the feed is read once, when the chat opens, and nothing polls
/// it afterwards, so without a push it goes on describing the chat as it stood at
/// page load while an event begins and finishes in front of the reader. The two
/// moments were already published to the bus by the domain service, but no
/// generator answered them: the queue binds exactly the events some generator
/// declares it can handle, so the broker dropped both without a trace.
///
/// Both are public facts about a public chat, so the recipient set is everyone
/// connected. As with a new global chat message,
/// <see cref="CreateNotification.UsersInterested"/> is left empty (nothing lands
/// in anyone's stored list, no letter and no bot message go out) and the
/// API-side realtime processor broadcasts these event types to every open
/// SignalR connection, guests included.
/// </remarks>
internal class GlobalChatEventLifecycleNotificationGenerator : INotificationGenerator
{
    private static readonly EventType[] SupportedTypes =
    {
        EventType.GlobalChatEventStarted,
        EventType.GlobalChatEventEnded
    };

    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GlobalChatEventLifecycleNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public bool CanResolve(EventType eventType) => SupportedTypes.Contains(eventType);

    /// <inheritdoc />
    public async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        // The event carries no idempotency key and may be redelivered, by which
        // time the subject can be gone: telling every open tab to re-read the
        // list because of an event that no longer exists is noise.
        var exists = await _dbContext.GlobalChatEvents
            .AnyAsync(e => e.GlobalChatEventId == entityId);

        if (!exists)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            // Empty on purpose: fan-out to all connected clients happens in the
            // API realtime processor, not through per-user stored notifications
            // (which would put a row about a public event in every inbox)
            UsersInterested = [],
            // No ActorId: an organizer does press start and end, but the row keeps
            // only StartedUtc and EndedUtc, never who moved the switch, and the
            // creator it does store is not necessarily that person. Nothing to
            // filter against either, the recipient set being the whole chat.
            Metadata = new
            {
                GlobalChatEventId = entityId
            }
        };
    }
}
